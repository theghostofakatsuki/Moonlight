﻿using System.Net.WebSockets;
using MoonCore.Helpers;
using Moonlight.Features.Servers.Api.Packets;
using Moonlight.Features.Servers.Entities;
using Moonlight.Features.Servers.Models.Abstractions;
using Moonlight.Features.Servers.Models.Enums;

namespace Moonlight.Features.Servers.Helpers;

public class ServerConsole
{
    public SmartEventHandler<ServerState> OnStateChange { get; set; } = new();
    public SmartEventHandler<ServerStats> OnStatsChange { get; set; } = new();
    public SmartEventHandler<string> OnNewMessage { get; set; } = new();
    public SmartEventHandler OnDisconnected { get; set; } = new();

    public ServerState State { get; private set; } = ServerState.Offline;
    public ServerStats Stats { get; private set; } = new();
    public DateTime LastStateChangeTimestamp { get; private set; } = DateTime.UtcNow;
    public string[] Messages => GetMessageCache();

    private readonly List<string> MessageCache = new();
    private readonly Server Server;

    private ClientWebSocket WebSocket;
    private WsPacketConnection PacketConnection;

    private CancellationTokenSource Cancellation = new();

    public ServerConsole(Server server)
    {
        if (server.Node == null)
            throw new ArgumentNullException(nameof(server.Node));

        Server = server;
    }

    public async Task Connect()
    {
        WebSocket = new();

        string wsUrl;

        if (Server.Node.Ssl)
            wsUrl = $"wss://{Server.Node.Fqdn}:{Server.Node.HttpPort}/servers/{Server.Id}/ws";
        else
            wsUrl = $"ws://{Server.Node.Fqdn}:{Server.Node.HttpPort}/servers/{Server.Id}/ws";

        await WebSocket.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
        PacketConnection = new WsPacketConnection(WebSocket);

        await PacketConnection.RegisterPacket<string>("output");
        await PacketConnection.RegisterPacket<ServerState>("state");
        await PacketConnection.RegisterPacket<ServerStats>("stats");

        Task.Run(Worker);
    }

    private async Task Worker()
    {
        while (!Cancellation.IsCancellationRequested && WebSocket.State == WebSocketState.Open)
        {
            try
            {
                var packet = await PacketConnection.Receive();

                if (packet == null)
                    continue;

                if (packet is string message)
                {
                    lock (MessageCache)
                    {
                        if (MessageCache.Count > 1000)
                            MessageCache.RemoveRange(0, 500);

                        MessageCache.Add(message);
                    }

                    await OnNewMessage.Invoke(message);
                }

                if (packet is ServerState state)
                {
                    State = state;
                    LastStateChangeTimestamp = DateTime.UtcNow;

                    await OnStateChange.Invoke(state);
                }

                if (packet is ServerStats stats)
                {
                    Stats = stats;

                    await OnStatsChange.Invoke(stats);
                }
            }
            catch (Exception e)
            {
                if (e is not WebSocketException)
                {
                    Logger.Warn("Lost connection to daemon server websocket");
                    Logger.Warn(e);
                }
                
                break;
            }
        }

        await OnDisconnected.Invoke();
        await PacketConnection.Close();
    }

    public async Task Close()
    {
        if(!Cancellation.IsCancellationRequested)
            Cancellation.Cancel();
        
        if(PacketConnection != null)
            await PacketConnection.Close();
    }

    private string[] GetMessageCache()
    {
        lock (MessageCache)
            return MessageCache.ToArray();
    }
}