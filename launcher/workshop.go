package launcher

import (
	"archive/zip"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strings"
	"time"
)

const maxWorkshopArchiveBytes int64 = 8 << 30
const maxWorkshopExtractBytes int64 = 24 << 30

func (a *App) reloadWorkshopMaps() {
	path := filepath.Join(a.dataDir, "workshop_maps.json")
	var maps []WorkshopMapItem
	if err := readJSON(path, &maps); err == nil {
		a.workshopMaps = maps
	}
	a.refreshWorkshopInstalledLocked()
}

func (a *App) saveWorkshopMapsLocked() error {
	data, err := json.MarshalIndent(a.workshopMaps, "", "  ")
	if err != nil { return err }
	return atomicWrite(filepath.Join(a.dataDir, "workshop_maps.json"), data)
}

func (a *App) refreshWorkshopInstalledLocked() {
	if strings.TrimSpace(a.settings.WorkshopPath) == "" {
		for i := range a.workshopMaps {
			a.workshopMaps[i].IsInstalled = false
			a.workshopMaps[i].LocalPath = ""
		}
		return
	}
	root := filepath.Join(a.settings.WorkshopPath, "content", "730")
	for i := range a.workshopMaps {
		destination := filepath.Join(root, fmt.Sprint(a.workshopMaps[i].WorkshopId))
		info, err := os.Stat(destination)
		a.workshopMaps[i].IsInstalled = err == nil && info.IsDir()
		a.workshopMaps[i].LocalPath = destination
	}
}

func (a *App) loadACFURL() {
	path := filepath.Join(a.dataDir, "acf.json")
	data, err := os.ReadFile(path)
	if err != nil { return }
	var object map[string]any
	if err := json.Unmarshal(data, &object); err == nil {
		if value, ok := object["acfUrl"].(string); ok { a.acfURL = strings.TrimSpace(value); return }
	}
	var array []map[string]any
	if err := json.Unmarshal(data, &array); err == nil && len(array) > 0 {
		if value, ok := array[0]["acfUrl"].(string); ok { a.acfURL = strings.TrimSpace(value) }
	}
}

func (a *App) SetWorkshopPath(path string) error {
	path = strings.TrimSpace(path)
	if path == "" { return errors.New("choose a workshop directory") }
	info, err := os.Stat(path)
	if err != nil || !info.IsDir() { return errors.New("workshop directory does not exist") }
	contentDir := filepath.Join(path, "content", "730")
	if err := os.MkdirAll(contentDir, 0755); err != nil { return err }
	a.mu.Lock()
	defer a.mu.Unlock()
	a.settings.WorkshopPath = path
	a.refreshWorkshopInstalledLocked()
	a.message = "Workshop directory updated."
	return a.saveLocked()
}

func (a *App) RefreshWorkshopMaps() {
	a.mu.Lock()
	a.reloadWorkshopMaps()
	a.mu.Unlock()
}

func (a *App) StartWorkshopDownload(workshopID int64) error {
	a.mu.Lock()
	if a.workshopCancel != nil {
		a.mu.Unlock()
		return errors.New("a workshop download is already running")
	}
	if strings.TrimSpace(a.settings.WorkshopPath) == "" {
		a.mu.Unlock()
		return errors.New("set your CS2 workshop directory first")
	}
	var selected *WorkshopMapItem
	for i := range a.workshopMaps {
		if a.workshopMaps[i].WorkshopId == workshopID {
			copy := a.workshopMaps[i]
			selected = &copy
			break
		}
	}
	if selected == nil {
		a.mu.Unlock()
		return errors.New("workshop map not found in workshop_maps.json")
	}
	if err := validateHTTPURL(selected.DownloadUrl); err != nil {
		a.mu.Unlock()
		return fmt.Errorf("map has no valid download URL: %w", err)
	}
	ctx, cancel := context.WithCancel(context.Background())
	a.workshopCancel = cancel
	a.workshopDownload = WorkshopDownloadState{
		Active: true, WorkshopID: workshopID, Percent: 0, Message: "Preparing download...",
	}
	workshopPath := a.settings.WorkshopPath
	a.message = "Workshop download started."
	a.mu.Unlock()

	go func(item WorkshopMapItem) {
		err := downloadAndInstallWorkshopMap(ctx, item, workshopPath, func(received, total int64) {
			a.mu.Lock()
			if a.workshopDownload.WorkshopID == item.WorkshopId {
				a.workshopDownload.Received = received
				a.workshopDownload.Total = total
				if total > 0 {
					a.workshopDownload.Percent = int(received * 100 / total)
					if a.workshopDownload.Percent > 99 { a.workshopDownload.Percent = 99 }
				}
				a.workshopDownload.Message = fmt.Sprintf("Downloading %s · %.1f MB", item.MapName, float64(received)/(1<<20))
			}
			a.mu.Unlock()
		}, func(message string) {
			a.mu.Lock()
			if a.workshopDownload.WorkshopID == item.WorkshopId {
				a.workshopDownload.Message = message
			}
			a.mu.Unlock()
		})
		a.mu.Lock()
		a.workshopCancel = nil
		a.workshopDownload.Active = false
		if err != nil {
			if errors.Is(err, context.Canceled) {
				a.workshopDownload.Message = "Download cancelled."
				a.message = "Workshop download cancelled."
			} else {
				a.workshopDownload.Message = "Download failed: " + err.Error()
				a.message = a.workshopDownload.Message
			}
		} else {
			a.workshopDownload.Completed = true
			a.workshopDownload.Percent = 100
			a.workshopDownload.Message = "Map downloaded and installed. Install/update ACF separately if required."
			a.message = item.MapName + " downloaded and installed."
			a.refreshWorkshopInstalledLocked()
		}
		a.mu.Unlock()
	}(*selected)
	return nil
}

func (a *App) CancelWorkshopDownload() {
	a.mu.Lock()
	cancel := a.workshopCancel
	a.mu.Unlock()
	if cancel != nil { cancel() }
}

func (a *App) InstallACF(ctx context.Context) error {
	a.mu.RLock()
	urlValue, workshopPath := a.acfURL, a.settings.WorkshopPath
	a.mu.RUnlock()
	if err := validateHTTPURL(urlValue); err != nil {
		return errors.New("acf.json must contain a valid acfUrl")
	}
	if workshopPath == "" {
		return errors.New("set your CS2 workshop directory first")
	}
	destination := filepath.Join(workshopPath, "appworkshop_730.acf")
	if err := downloadToFile(ctx, urlValue, destination, 32<<20, nil); err != nil { return err }
	a.setMessage("Workshop ACF file installed.")
	return nil
}

func (a *App) CopyPreparedACF() error {
	a.mu.RLock()
	workshopPath := a.settings.WorkshopPath
	a.mu.RUnlock()
	if workshopPath == "" { return nil }
	source := filepath.Join(a.dataDir, "appworkshop_730.acf")
	if _, err := os.Stat(source); err != nil { return nil }
	destination := filepath.Join(workshopPath, "appworkshop_730.acf")
	input, err := os.ReadFile(source)
	if err != nil { return err }
	return atomicWrite(destination, input)
}

func validateHTTPURL(value string) error {
	parsed, err := url.ParseRequestURI(strings.TrimSpace(value))
	if err != nil { return err }
	if parsed.Scheme != "http" && parsed.Scheme != "https" {
		return errors.New("only HTTP and HTTPS download URLs are supported")
	}
	if parsed.Host == "" { return errors.New("URL has no host") }
	return nil
}

func downloadToFile(ctx context.Context, source, destination string, maximum int64, progress func(int64, int64)) error {
	if err := validateHTTPURL(source); err != nil { return err }
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, source, nil)
	if err != nil { return err }
	client := &http.Client{Timeout: 0}
	response, err := client.Do(request)
	if err != nil { return err }
	defer response.Body.Close()
	if response.StatusCode < 200 || response.StatusCode > 299 {
		return fmt.Errorf("download returned HTTP %d", response.StatusCode)
	}
	if response.ContentLength > maximum {
		return fmt.Errorf("download is too large (%d bytes)", response.ContentLength)
	}
	if err := os.MkdirAll(filepath.Dir(destination), 0755); err != nil { return err }
	temp, err := os.CreateTemp(filepath.Dir(destination), ".fragcity-download-*")
	if err != nil { return err }
	tempName := temp.Name()
	defer os.Remove(tempName)
	reader := &io.LimitedReader{R: response.Body, N: maximum + 1}
	buffer := make([]byte, 128*1024)
	var received int64
	for {
		n, readErr := reader.Read(buffer)
		if n > 0 {
			written, writeErr := temp.Write(buffer[:n])
			received += int64(written)
			if writeErr != nil { _ = temp.Close(); return writeErr }
			if progress != nil { progress(received, response.ContentLength) }
		}
		if readErr == io.EOF { break }
		if readErr != nil { _ = temp.Close(); return readErr }
		if received > maximum { _ = temp.Close(); return errors.New("download exceeded configured size limit") }
	}
	if err := temp.Sync(); err != nil { _ = temp.Close(); return err }
	if err := temp.Close(); err != nil { return err }
	return os.Rename(tempName, destination)
}

func downloadAndInstallWorkshopMap(ctx context.Context, item WorkshopMapItem, workshopPath string, progress func(int64, int64), setMessage func(string)) error {
	temp, err := os.CreateTemp("", "fragcity-workshop-*.zip")
	if err != nil { return err }
	tempPath := temp.Name()
	_ = temp.Close()
	defer os.Remove(tempPath)
	if err := downloadToFileWithProgress(ctx, item.DownloadUrl, tempPath, maxWorkshopArchiveBytes, progress); err != nil { return err }

	if setMessage != nil { setMessage("Checking archive and extracting map files...") }
	contentRoot := filepath.Join(workshopPath, "content", "730")
	if err := os.MkdirAll(contentRoot, 0755); err != nil { return err }
	destination := filepath.Join(contentRoot, fmt.Sprint(item.WorkshopId))
	staging, err := os.MkdirTemp(contentRoot, fmt.Sprintf(".%d-install-*", item.WorkshopId))
	if err != nil { return err }
	defer os.RemoveAll(staging)
	if err := extractZipSafely(tempPath, staging); err != nil { return err }

	// Older map packages wrap their files in a second folder named by Workshop ID.
	nested := filepath.Join(staging, fmt.Sprint(item.WorkshopId))
	if info, statErr := os.Stat(nested); statErr == nil && info.IsDir() {
		entries, readErr := os.ReadDir(staging)
		if readErr == nil && len(entries) == 1 {
			for _, entry := range entries {
				if entry.IsDir() && entry.Name() == fmt.Sprint(item.WorkshopId) {
					flatten := staging + "-flatten"
					if moveErr := os.Rename(nested, flatten); moveErr == nil {
						_ = os.RemoveAll(staging)
						staging = flatten
						defer os.RemoveAll(staging)
					}
				}
			}
		}
	}
	backup := destination + ".previous"
	_ = os.RemoveAll(backup)
	if _, statErr := os.Stat(destination); statErr == nil {
		if err := os.Rename(destination, backup); err != nil { return err }
	}
	if err := os.Rename(staging, destination); err != nil {
		if _, statErr := os.Stat(backup); statErr == nil { _ = os.Rename(backup, destination) }
		return err
	}
	_ = os.RemoveAll(backup)
	return nil
}

func downloadToFileWithProgress(ctx context.Context, source, destination string, maximum int64, progress func(int64, int64)) error {
	if err := validateHTTPURL(source); err != nil { return err }
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, source, nil)
	if err != nil { return err }
	response, err := (&http.Client{Timeout: 0}).Do(request)
	if err != nil { return err }
	defer response.Body.Close()
	if response.StatusCode < 200 || response.StatusCode > 299 { return fmt.Errorf("download returned HTTP %d", response.StatusCode) }
	if response.ContentLength > maximum { return errors.New("workshop archive exceeds 8 GB limit") }
	file, err := os.Create(destination)
	if err != nil { return err }
	defer file.Close()
	reader := &io.LimitedReader{R: response.Body, N: maximum + 1}
	buffer := make([]byte, 128*1024)
	var received int64
	for {
		n, readErr := reader.Read(buffer)
		if n > 0 {
			written, writeErr := file.Write(buffer[:n])
			received += int64(written)
			if writeErr != nil { return writeErr }
			if progress != nil { progress(received, response.ContentLength) }
		}
		if readErr == io.EOF { break }
		if readErr != nil { return readErr }
		if received > maximum { return errors.New("workshop archive exceeds 8 GB limit") }
	}
	if err := file.Sync(); err != nil { return err }
	return nil
}

func extractZipSafely(source, destination string) error {
	archive, err := zip.OpenReader(source)
	if err != nil { return fmt.Errorf("open ZIP archive: %w", err) }
	defer archive.Close()
	root, err := filepath.Abs(destination)
	if err != nil { return err }
	var extracted int64
	for _, item := range archive.File {
		if item.FileInfo().Mode()&os.ModeSymlink != 0 {
			return errors.New("archive contains a symbolic link; extraction refused")
		}
		clean := filepath.Clean(filepath.FromSlash(item.Name))
		if clean == "." && item.FileInfo().IsDir() {
			continue
		}
		if clean == "." || filepath.IsAbs(clean) || clean == ".." || strings.HasPrefix(clean, ".."+string(os.PathSeparator)) {
			return fmt.Errorf("archive contains an unsafe path: %q", item.Name)
		}
		target := filepath.Join(root, clean)
		rel, err := filepath.Rel(root, target)
		if err != nil || rel == ".." || strings.HasPrefix(rel, ".."+string(os.PathSeparator)) {
			return fmt.Errorf("archive path escapes destination: %q", item.Name)
		}
		if item.FileInfo().IsDir() {
			if err := os.MkdirAll(target, 0755); err != nil { return err }
			continue
		}
		if item.UncompressedSize64 > uint64(maxWorkshopExtractBytes) {
			return errors.New("archive entry exceeds extraction limit")
		}
		extracted += int64(item.UncompressedSize64)
		if extracted > maxWorkshopExtractBytes {
			return errors.New("archive expands beyond 24 GB extraction limit")
		}
		if err := os.MkdirAll(filepath.Dir(target), 0755); err != nil { return err }
		input, err := item.Open()
		if err != nil { return err }
		output, err := os.OpenFile(target, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0644)
		if err != nil { input.Close(); return err }
		_, copyErr := io.CopyN(output, input, int64(item.UncompressedSize64))
		closeInErr, closeOutErr := input.Close(), output.Close()
		if copyErr != nil { return copyErr }
		if closeInErr != nil { return closeInErr }
		if closeOutErr != nil { return closeOutErr }
	}
	return nil
}

func (a *App) CheckUpdate(ctx context.Context) (UpdateInfo, error) {
	a.mu.RLock()
	checkURL := a.settings.UpdateCheckUrl
	if checkURL == "" { checkURL = a.settings.VersionCheckUrl }
	current := a.settings.CurrentVersion
	a.mu.RUnlock()
	if checkURL == "" { return UpdateInfo{}, errors.New("update check URL is not configured") }
	if err := validateHTTPURL(checkURL); err != nil { return UpdateInfo{}, err }
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, checkURL, nil)
	if err != nil { return UpdateInfo{}, err }
	response, err := (&http.Client{Timeout: 15 * time.Second}).Do(request)
	if err != nil { return UpdateInfo{}, err }
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK { return UpdateInfo{}, fmt.Errorf("update server returned HTTP %d", response.StatusCode) }
	var latest UpdateInfo
	if err := json.NewDecoder(io.LimitReader(response.Body, 128<<10)).Decode(&latest); err != nil { return UpdateInfo{}, err }
	if latest.Version == "" { return UpdateInfo{}, errors.New("update server returned no version") }
	if latest.DownloadUrl != "" {
		if err := validateHTTPURL(latest.DownloadUrl); err != nil { return UpdateInfo{}, fmt.Errorf("invalid update download URL: %w", err) }
	}
	if !isNewerVersion(latest.Version, current) { latest.DownloadUrl = "" }
	return latest, nil
}

func isNewerVersion(latest, current string) bool {
	parse := func(value string) []int {
		value = strings.TrimPrefix(strings.TrimSpace(value), "v")
		parts := strings.Split(value, ".")
		result := make([]int, 0, len(parts))
		for _, part := range parts {
			digits := strings.TrimRightFunc(part, func(r rune) bool { return r < '0' || r > '9' })
			n := 0
			_, _ = fmt.Sscanf(digits, "%d", &n)
			result = append(result, n)
		}
		return result
	}
	a, b := parse(latest), parse(current)
	for len(a) < len(b) { a = append(a, 0) }
	for len(b) < len(a) { b = append(b, 0) }
	for i := range a {
		if a[i] > b[i] { return true }
		if a[i] < b[i] { return false }
	}
	return false
}

func (a *App) DownloadUpdate(ctx context.Context, info UpdateInfo) (string, error) {
	if info.Version == "" { return "", errors.New("missing update version") }
	if err := validateHTTPURL(info.DownloadUrl); err != nil { return "", err }
	temp := filepath.Join(os.TempDir(), "fragcity-update-"+fmt.Sprint(time.Now().UnixNano())+".zip")
	if err := downloadToFile(ctx, info.DownloadUrl, temp, maxWorkshopArchiveBytes, nil); err != nil { return "", err }
	defer os.Remove(temp)
	destination := filepath.Join(a.dataDir, "UpdatePackage")
	staging, err := os.MkdirTemp(a.dataDir, ".update-staging-*")
	if err != nil { return "", err }
	defer os.RemoveAll(staging)
	if err := extractZipSafely(temp, staging); err != nil { return "", err }
	_ = os.RemoveAll(destination)
	if err := os.Rename(staging, destination); err != nil { return "", err }
	a.mu.Lock()
	a.settings.CurrentVersion = info.Version
	err = a.saveLocked()
	a.mu.Unlock()
	if err != nil { return "", err }
	return destination, nil
}

func (u *UpdateInfo) UnmarshalJSON(data []byte) error {
	var values map[string]json.RawMessage
	if err := json.Unmarshal(data, &values); err != nil { return err }
	for key, raw := range values {
		switch strings.ToLower(key) {
		case "version":
			_ = json.Unmarshal(raw, &u.Version)
		case "downloadurl", "download_url":
			_ = json.Unmarshal(raw, &u.DownloadUrl)
		}
	}
	return nil
}
