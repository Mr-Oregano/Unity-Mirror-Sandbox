
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Mirror;

public class Room
{
	public RoomInfo m_Info;

	public Scene GameScene{ get; private set; }

	public List<NetworkConnection> Clients { get; private set; }
	
	public NetworkConnection Host { get; private set; }

	public bool OnGoing { get; private set; }

	public Room(NetworkConnection host)
	{
		Host = host;
		Clients = new List<NetworkConnection>();
		AddClientConnection(host); // add host to connections by default
	}

	public void AddClientConnection(NetworkConnection conn)
	{
		Clients.Add(conn);
	}

	public bool IsHost(NetworkConnection conn)
	{
		return Host.connectionId == conn.connectionId;
	}

	public void SetScene(Scene scene)
	{
		GameScene = scene;
		OnGoing = true;
	}
}

// NOTE: The room info struct exists for the clients to receive info
//		 without receiving the network connections of other players.
//
public struct RoomInfo
{
	public string sceneName;
	public List<PlayerInfo> playerInfos;
	public int code;
	public bool isLocalPlayerHost; // TODO: Find a better way to track the room host
}