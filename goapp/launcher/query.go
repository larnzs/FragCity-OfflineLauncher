package launcher

import (
	"bytes"
	"context"
	"encoding/binary"
	"errors"
	"fmt"
	"net"
	"strconv"
	"time"
)

var a2sInfoRequest = []byte{
	0xFF, 0xFF, 0xFF, 0xFF, 0x54,
	'S', 'o', 'u', 'r', 'c', 'e', ' ', 'E', 'n', 'g', 'i', 'n', 'e', ' ', 'Q', 'u', 'e', 'r', 'y', 0,
}

func dialQuery(ctx context.Context, ip string, port int) (*net.UDPConn, error) {
	if net.ParseIP(ip) == nil { return nil, fmt.Errorf("invalid server IP %q", ip) }
	address, err := net.ResolveUDPAddr("udp", net.JoinHostPort(ip, strconv.Itoa(port)))
	if err != nil { return nil, err }
	conn, err := net.DialUDP("udp", nil, address)
	if err != nil { return nil, err }
	deadline := time.Now().Add(2 * time.Second)
	if ctxDeadline, ok := ctx.Deadline(); ok && ctxDeadline.Before(deadline) { deadline = ctxDeadline }
	_ = conn.SetDeadline(deadline)
	return conn, nil
}

func exchangeInfo(conn *net.UDPConn) ([]byte, error) {
	if _, err := conn.Write(a2sInfoRequest); err != nil { return nil, err }
	response := make([]byte, 65535)
	n, err := conn.Read(response)
	if err != nil { return nil, err }
	packet := response[:n]
	if len(packet) < 5 || binary.LittleEndian.Uint32(packet[:4]) != 0xFFFFFFFF {
		return nil, errors.New("invalid A2S_INFO response")
	}
	if packet[4] == 0x41 {
		if len(packet) < 9 { return nil, errors.New("short A2S challenge response") }
		request := append(append([]byte(nil), a2sInfoRequest...), packet[5:9]...)
		if _, err := conn.Write(request); err != nil { return nil, err }
		n, err = conn.Read(response)
		if err != nil { return nil, err }
		packet = response[:n]
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
		if err != nil { return "", err }
		if b == 0 { return string(value), nil }
		value = append(value, b)
		if len(value) > 4096 { return "", errors.New("A2S string is too long") }
	}
}

func QueryServerInfo(ctx context.Context, ip string, port int) (ServerInfo, error) {
	conn, err := dialQuery(ctx, ip, port)
	if err != nil { return ServerInfo{}, err }
	defer conn.Close()
	started := time.Now()
	packet, err := exchangeInfo(conn)
	if err != nil { return ServerInfo{}, err }
	reader := bytes.NewReader(packet[5:])
	_, err = reader.ReadByte() // protocol
	if err != nil { return ServerInfo{}, err }

	name, err := readCString(reader); if err != nil { return ServerInfo{}, err }
	mapName, err := readCString(reader); if err != nil { return ServerInfo{}, err }
	_, err = readCString(reader); if err != nil { return ServerInfo{}, err } // folder
	_, err = readCString(reader); if err != nil { return ServerInfo{}, err } // game
	var appID uint16
	if err := binary.Read(reader, binary.LittleEndian, &appID); err != nil { return ServerInfo{}, err }
	players, err := reader.ReadByte(); if err != nil { return ServerInfo{}, err }
	maxPlayers, err := reader.ReadByte(); if err != nil { return ServerInfo{}, err }
	_, err = reader.ReadByte(); if err != nil { return ServerInfo{}, err } // bots
	_, err = reader.ReadByte(); if err != nil { return ServerInfo{}, err } // server type
	_, err = reader.ReadByte(); if err != nil { return ServerInfo{}, err } // OS
	password, err := reader.ReadByte(); if err != nil { return ServerInfo{}, err }
	vac, err := reader.ReadByte(); if err != nil { return ServerInfo{}, err }

	return ServerInfo{
		Name: name, Map: mapName, Players: int(players), MaxPlayers: int(maxPlayers),
		HasPassword: password == 1, IsSecure: vac == 1, Ping: int(time.Since(started).Milliseconds()),
	}, nil
}

func QueryPlayers(ctx context.Context, ip string, port int) ([]PlayerInfo, error) {
	conn, err := dialQuery(ctx, ip, port)
	if err != nil { return nil, err }
	defer conn.Close()

	challengeRequest := []byte{0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF}
	if _, err := conn.Write(challengeRequest); err != nil { return nil, err }
	buffer := make([]byte, 65535)
	n, err := conn.Read(buffer)
	if err != nil { return nil, err }
	response := buffer[:n]
	if len(response) < 9 || response[4] != 0x41 {
		return nil, errors.New("invalid A2S_PLAYER challenge response")
	}

	request := make([]byte, 9)
	copy(request[:5], challengeRequest[:5])
	copy(request[5:], response[5:9])
	if _, err := conn.Write(request); err != nil { return nil, err }
	n, err = conn.Read(buffer)
	if err != nil { return nil, err }
	response = buffer[:n]
	if len(response) < 6 || binary.LittleEndian.Uint32(response[:4]) != 0xFFFFFFFF || response[4] != 0x44 {
		return nil, errors.New("invalid A2S_PLAYER response")
	}
	reader := bytes.NewReader(response[5:])
	count, err := reader.ReadByte()
	if err != nil { return nil, err }
	players := make([]PlayerInfo, 0, int(count))
	for i := 0; i < int(count); i++ {
		index, err := reader.ReadByte(); if err != nil { return nil, err }
		name, err := readCString(reader); if err != nil { return nil, err }
		var score int32
		var duration float32
		if err := binary.Read(reader, binary.LittleEndian, &score); err != nil { return nil, err }
		if err := binary.Read(reader, binary.LittleEndian, &duration); err != nil { return nil, err }
		players = append(players, PlayerInfo{Index: int(index), Name: name, Score: int(score), Duration: duration})
	}
	return players, nil
}
