package launcher

import (
	"archive/zip"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"
)

func TestUpdateInfoAcceptsOriginalSnakeCaseManifest(t *testing.T) {
	var info UpdateInfo
	if err := json.Unmarshal([]byte("{\"version\":\"1.7.0\",\"download_url\":\"https://example.invalid/release.zip\"}"), &info); err != nil {
		t.Fatal(err)
	}
	if info.Version != "1.7.0" || info.DownloadUrl != "https://example.invalid/release.zip" {
		t.Fatalf("unexpected update manifest: %#v", info)
	}
}

func TestIsNewerVersion(t *testing.T) {
	if !isNewerVersion("1.7.0", "1.6.0") { t.Fatal("newer patch should compare as newer") }
	if isNewerVersion("1.6.0", "1.6.0") { t.Fatal("equal versions are not newer") }
	if isNewerVersion("1.5.9", "1.6.0") { t.Fatal("older version should not compare as newer") }
}

func TestExtractZipSafelyRejectsPathTraversal(t *testing.T) {
	dir := t.TempDir()
	archivePath := filepath.Join(dir, "bad.zip")
	file, err := os.Create(archivePath)
	if err != nil { t.Fatal(err) }
	writer := zip.NewWriter(file)
	entry, err := writer.Create("../outside.txt")
	if err != nil { t.Fatal(err) }
	if _, err := entry.Write([]byte("bad")); err != nil { t.Fatal(err) }
	if err := writer.Close(); err != nil { t.Fatal(err) }
	if err := file.Close(); err != nil { t.Fatal(err) }

	err = extractZipSafely(archivePath, filepath.Join(dir, "output"))
	if err == nil { t.Fatal("expected traversal archive to be rejected") }
	if _, statErr := os.Stat(filepath.Join(dir, "outside.txt")); !os.IsNotExist(statErr) {
		t.Fatal("archive wrote a file outside the extraction directory")
	}
}

func TestExtractZipSafelyExtractsNormalArchive(t *testing.T) {
	dir := t.TempDir()
	archivePath := filepath.Join(dir, "good.zip")
	file, err := os.Create(archivePath)
	if err != nil { t.Fatal(err) }
	writer := zip.NewWriter(file)
	entry, err := writer.Create("maps/de_dust2/vmap_c")
	if err != nil { t.Fatal(err) }
	if _, err := entry.Write([]byte("mapdata")); err != nil { t.Fatal(err) }
	if err := writer.Close(); err != nil { t.Fatal(err) }
	if err := file.Close(); err != nil { t.Fatal(err) }

	output := filepath.Join(dir, "output")
	if err := extractZipSafely(archivePath, output); err != nil { t.Fatal(err) }
	got, err := os.ReadFile(filepath.Join(output, "maps", "de_dust2", "vmap_c"))
	if err != nil { t.Fatal(err) }
	if string(got) != "mapdata" { t.Fatalf("unexpected extracted bytes: %q", got) }
}
