package launcher

import (
	"bytes"
	"context"
	"encoding/binary"
	"errors"
	"fmt"
	"net"
	"strconv"
	"strings"
	"time"
)

var a2sInfoRequest = []byte{
	0xFF, 0xFF, 0xFF, 0xFF, 0x54,
	'S', 'o', 'u', 'r', 'c', 'e', ' ', 'E', 'n', 'g', 'i', 'n', 'e', ' ', 'Q', 'u', 'e', 'r', 'y', 0,
}

func dialQuery(ctx context.Context, ip string, port int) (*net.UDPConn, error) {
	if net.ParseIP(ip) == nil {
		return nil, fmt.Errorf("invalid server IP %q", ip)
	}
	if port < 1 || port > 65535 {
		return nil, fmt.Errorf("invalid server port %d", port)
	}
	address, err := net.ResolveUDPAddr("udp", net.JoinHostPort(ip, strconv.Itoa(port)))
	if err != nil {
		return nil, err
	}
	conn, err := net.DialUDP("udp", nil, address)
	if err != nil {
		return nil, err
	}
	deadline := time.Now().Add(2 * time.Second)
	if ctxDeadline, ok := ctx.Deadline(); ok && ctxDeadline.Before(deadline) {
		deadline = ctxDeadline
	}
	_ = conn.SetDeadline(deadline)
	return conn, nil
}

func readDatagram(conn *net.UDPConn) ([]byte, error) {
	buffer := make([]byte, 65535)
	n, err := conn.Read(buffer)
	if err != nil {
		return nil, err
	}
	return buffer[:n], nil
}

func exchangeInfo(conn *net.UDPConn) ([]byte, error) {
	if _, err := conn.Write(a2sInfoRequest); err != nil {
		return nil, err
	}
	packet, err := readDatagram(conn)
	if err != nil {
		return nil, err
	}
	if len(packet) < 5 || binary.LittleEndian.Uint32(packet[:4]) != 0xFFFFFFFF {
		return nil, errors.New("invalid A2S_INFO response")
	}
	if packet[4] == 0x41 {
		if len(packet) < 9 {
			return nil, errors.New("short A2S challenge response")
		}
		request := append(append([]byte(nil), a2sInfoRequest...), packet[5:9]...)
		if _, err := conn.Write(request); err != nil {
			return nil, err
		}
		packet, err = readDatagram(conn)
		if err != nil {
			return nil, err
		}
	}
	if len(packet) < 6 || binary.LittleEndian.Uint32(packet[:4]) != 0xFFFFFFFF || packet[4] != 0x49 {
		return nil, errors.New("server returned an unsupported A2S_INFO packet")
	}
	return packet, nil
}

func readCString(r *bytes.Reader) (string, error) {
	value := make([]byte, 0, 64)
	for {
		b, err := r.ReadByte()
		if err != nil {
			return "", err
		}
		if b == 0 {
			return string(value), nil
		}
		value = append(value, b)
		if len(value) > 4096 {
			return "", errors.New("A2S string is too long")
		}
	}
}

func QueryServerInfo(ctx context.Context, ip string, port int) (ServerInfo, error) {
	conn, err := dialQuery(ctx, ip, port)
	if err != nil {
		return ServerInfo{}, err
	}
	defer conn.Close()

	started := time.Now()
	packet, err := exchangeInfo(conn)
	if err != nil {
		return ServerInfo{}, err
	}
	reader := bytes.NewReader(packet[5:])
	if _, err = reader.ReadByte(); err != nil {
		return ServerInfo{}, err
	}
	name, err := readCString(reader)
	if err != nil {
		return ServerInfo{}, err
	}
	mapName, err := readCString(reader)
	if err != nil {
		return ServerInfo{}, err
	}
	_, err = readCString(reader) // folder
	if err != nil {
		return ServerInfo{}, err
	}
	_, err = readCString(reader) // game
	if err != nil {
		return ServerInfo{}, err
	}
	var appID uint16
	if err := binary.Read(reader, binary.LittleEndian, &appID); err != nil {
		return ServerInfo{}, err
	}
	players, err := reader.ReadByte()
	if err != nil {
		return ServerInfo{}, err
	}
	maxPlayers, err := reader.ReadByte()
	if err != nil {
		return ServerInfo{}, err
	}
	if _, err = reader.ReadByte(); err != nil { // bots
		return ServerInfo{}, err
	}
	if _, err = reader.ReadByte(); err != nil { // server type
		return ServerInfo{}, err
	}
	if _, err = reader.ReadByte(); err != nil { // OS
		return ServerInfo{}, err
	}
	password, err := reader.ReadByte()
	if err != nil {
		return ServerInfo{}, err
	}
	vac, err := reader.ReadByte()
	if err != nil {
		return ServerInfo{}, err
	}
	version, _ := readCString(reader)
	_ = version

	return ServerInfo{
		Name: name, Map: mapName, Players: int(players), MaxPlayers: int(maxPlayers),
		HasPassword: password == 1, IsSecure: vac == 1, Ping: int(time.Since(started).Milliseconds()),
	}, nil
}

// QueryServerRules supports the Source A2S_RULES challenge/response exchange.
// Some older server builds use 0x56 for the rules payload; current servers use 0x45.
func QueryServerRules(ctx context.Context, ip string, port int) (map[string]string, error) {
	conn, err := dialQuery(ctx, ip, port)
	if err != nil {
		return nil, err
	}
	defer conn.Close()

	request := []byte{0xFF, 0xFF, 0xFF, 0xFF, 0x56, 0xFF, 0xFF, 0xFF, 0xFF}
	if _, err := conn.Write(request); err != nil {
		return nil, err
	}
	packet, err := readDatagram(conn)
	if err != nil {
		return nil, err
	}
	if len(packet) < 5 || binary.LittleEndian.Uint32(packet[:4]) != 0xFFFFFFFF {
		return nil, errors.New("invalid A2S_RULES response")
	}
	if packet[4] == 0x41 {
		if len(packet) < 9 {
			return nil, errors.New("short A2S_RULES challenge response")
		}
		request = []byte{0xFF, 0xFF, 0xFF, 0xFF, 0x56}
		request = append(request, packet[5:9]...)
		if _, err := conn.Write(request); err != nil {
			return nil, err
		}
		packet, err = readDatagram(conn)
		if err != nil {
			return nil, err
		}
	}
	if len(packet) < 7 || binary.LittleEndian.Uint32(packet[:4]) != 0xFFFFFFFF {
		return nil, errors.New("invalid A2S_RULES payload")
	}
	offset := 5
	if packet[offset] == 0x45 {
		offset++
	} else if packet[offset] == 0x56 {
		offset++
	} else {
		return nil, fmt.Errorf("unexpected A2S_RULES type 0x%02x", packet[offset])
	}
	if len(packet)-offset < 2 {
		return nil, errors.New("short A2S_RULES count")
	}
	count := int(binary.LittleEndian.Uint16(packet[offset : offset+2]))
	offset += 2
	rules := make(map[string]string, count)
	readNull := func() (string, error) {
		start := offset
		for offset < len(packet) && packet[offset] != 0 {
			offset++
		}
		if offset >= len(packet) {
			return "", errors.New("unterminated A2S_RULES string")
		}
		value := string(packet[start:offset])
		offset++
		return value, nil
	}
	for i := 0; i < count && offset < len(packet); i++ {
		key, readErr := readNull()
		if readErr != nil {
			return nil, readErr
		}
		value, readErr := readNull()
		if readErr != nil {
			return nil, readErr
		}
		rules[key] = value
	}
	return rules, nil
}

func rulesInt(rules map[string]string, keys ...string) int {
	for key, value := range rules {
		for _, wanted := range keys {
			if strings.EqualFold(key, wanted) {
				if n, err := strconv.Atoi(strings.TrimSpace(value)); err == nil {
					return n
				}
			}
		}
	}
	return 0
}

func rulesString(rules map[string]string, fallback string, keys ...string) string {
	for key, value := range rules {
		for _, wanted := range keys {
			if strings.EqualFold(key, wanted) && strings.TrimSpace(value) != "" {
				return value
			}
		}
	}
	return fallback
}

func parseServerAddress(input string, defaultPort int) (string, int, error) {
	input = strings.TrimSpace(input)
	if input == "" {
		return "", 0, errors.New("enter an IP address or hostname")
	}
	host, port := input, defaultPort
	if h, p, err := net.SplitHostPort(input); err == nil {
		host = h
		parsed, parseErr := strconv.Atoi(p)
		if parseErr != nil || parsed < 1 || parsed > 65535 {
			return "", 0, errors.New("invalid port")
		}
		port = parsed
	} else if strings.Count(input, ":") == 1 {
		parts := strings.SplitN(input, ":", 2)
		if n, parseErr := strconv.Atoi(parts[1]); parseErr == nil {
			if n < 1 || n > 65535 {
				return "", 0, errors.New("invalid port")
			}
			host, port = parts[0], n
		}
	}
	if ip := net.ParseIP(strings.Trim(host, "[]")); ip != nil {
		return ip.String(), port, nil
	}
	addresses, err := net.LookupIP(host)
	if err != nil {
		return "", 0, fmt.Errorf("could not resolve %q: %w", host, err)
	}
	for _, address := range addresses {
		if v4 := address.To4(); v4 != nil {
			return v4.String(), port, nil
		}
	}
	if len(addresses) > 0 {
		return addresses[0].String(), port, nil
	}
	return "", 0, fmt.Errorf("no IP addresses found for %q", host)
}


// QueryPlayers implements the Source A2S_PLAYER challenge/response exchange.
func QueryPlayers(ctx context.Context, ip string, port int) ([]PlayerInfo, error) {
	conn, err := dialQuery(ctx, ip, port)
	if err != nil {
		return nil, err
	}
	defer conn.Close()

	challengeRequest := []byte{0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF}
	if _, err := conn.Write(challengeRequest); err != nil {
		return nil, err
	}

	challengeResponse, err := readDatagram(conn)
	if err != nil {
		return nil, err
	}
	if len(challengeResponse) < 9 ||
		binary.LittleEndian.Uint32(challengeResponse[:4]) != 0xFFFFFFFF ||
		challengeResponse[4] != 0x41 {
		return nil, errors.New("invalid A2S_PLAYER challenge response")
	}

	playerRequest := []byte{0xFF, 0xFF, 0xFF, 0xFF, 0x55}
	playerRequest = append(playerRequest, challengeResponse[5:9]...)
	if _, err := conn.Write(playerRequest); err != nil {
		return nil, err
	}

	playerResponse, err := readDatagram(conn)
	if err != nil {
		return nil, err
	}
	if len(playerResponse) < 6 ||
		binary.LittleEndian.Uint32(playerResponse[:4]) != 0xFFFFFFFF ||
		playerResponse[4] != 0x44 {
		return nil, errors.New("invalid A2S_PLAYER response")
	}

	reader := bytes.NewReader(playerResponse[5:])
	count, err := reader.ReadByte()
	if err != nil {
		return nil, fmt.Errorf("read A2S_PLAYER count: %w", err)
	}

	players := make([]PlayerInfo, 0, int(count))
	for i := 0; i < int(count); i++ {
		index, readErr := reader.ReadByte()
		if readErr != nil {
			return nil, fmt.Errorf("read player %d index: %w", i, readErr)
		}
		name, readErr := readCString(reader)
		if readErr != nil {
			return nil, fmt.Errorf("read player %d name: %w", i, readErr)
		}
		var score int32
		if readErr = binary.Read(reader, binary.LittleEndian, &score); readErr != nil {
			return nil, fmt.Errorf("read player %d score: %w", i, readErr)
		}
		var duration float32
		if readErr = binary.Read(reader, binary.LittleEndian, &duration); readErr != nil {
			return nil, fmt.Errorf("read player %d duration: %w", i, readErr)
		}
		players = append(players, PlayerInfo{
			Index: int(index), Name: name, Score: int(score), Duration: duration,
		})
	}
	return players, nil
}
