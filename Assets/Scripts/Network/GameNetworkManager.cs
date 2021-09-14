
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

public class GameNetworkManager : NetworkManager
{
	// Server Build
	private readonly Dictionary<int, Room> m_Rooms = new Dictionary<int, Room>();

	// Conn ID -> Room Code
	private readonly Dictionary<int, int> m_PlayersInRoom = new Dictionary<int, int>();

	private int m_NumLoadedScenes = 1;
	
	public override void OnStartServer()
	{
		base.OnStartServer();

		m_Rooms.Clear();

		NetworkServer.RegisterHandler<LoginRequest>(OnServerLogin);
		NetworkServer.RegisterHandler<RoomCreateRequest>(OnServerRoomCreate);
		NetworkServer.RegisterHandler<RoomJoinRequest>(OnServerRoomJoin);
		NetworkServer.RegisterHandler<RoomStartRequest>(OnServerRoomStart);
	}

	private void OnServerLogin(NetworkConnection conn, LoginRequest msg)
	{
		LoginResponse response = new LoginResponse();

		// TODO: Here is where you validate the credentials, for now we just use simple switch
		//		 and no password verification at all. But obviously you'd have some sophisticated
		//		 database for doing something like this.
		//
		response.success = true;
		switch (msg.username)
		{
			case "cheese": 	conn.authenticationData = "gay"; break;
			case "grapes": 	conn.authenticationData = "sam"; break;
			case "salt": 	conn.authenticationData = "tam"; break;
			case "salad": 	conn.authenticationData = "jim"; break;
			default: 		response.success = false; break;
		}

		conn.Send(response);
	}

	private void OnServerRoomCreate(NetworkConnection conn, RoomCreateRequest msg)
	{
		RoomJoinCreateResponse response = new RoomJoinCreateResponse();

		PlayerInfo info;
		if (!TryGetPlayerInfoFromConnection(conn, out info))
		{
			conn.Send(response);
			return;
		}

		// TODO: Generate a random unique room code.
		int newRoomCode = Random.Range(0, 99999);

		Room newRoom = new Room(conn);

		newRoom.m_Info.code = newRoomCode;
		newRoom.m_Info.playerInfos = new List<PlayerInfo>();
		newRoom.m_Info.playerInfos.Add(info);
		newRoom.m_Info.isLocalPlayerHost = true;
		m_Rooms.Add(newRoomCode, newRoom);
		m_PlayersInRoom.Add(conn.connectionId, newRoomCode);
		
		response.roomInfo = newRoom.m_Info;
		response.success = true;
		conn.Send(response);
	}

	private void OnServerRoomJoin(NetworkConnection conn, RoomJoinRequest msg)
	{
		RoomJoinCreateResponse response = new RoomJoinCreateResponse();

		Room room;
		PlayerInfo info;
		if (!m_Rooms.TryGetValue(msg.roomCode, out room) || !TryGetPlayerInfoFromConnection(conn, out info))
		{
			conn.Send(response);
			return;
		}

		room.AddClientConnection(conn);
		m_PlayersInRoom.Add(conn.connectionId, room.m_Info.code);

		room.m_Info.playerInfos.Add(info);
		room.m_Info.isLocalPlayerHost = false;

		response.roomInfo = room.m_Info;
		response.success = true;
		conn.Send(response);

		// NOTE: Send the update to all of the players in the room
		RoomNewUserJoinMessage userJoinMessage = new RoomNewUserJoinMessage();
		userJoinMessage.newPlayer = info;

		foreach (NetworkConnection c in room.Clients)
			if (c.connectionId != conn.connectionId)
				c.Send(userJoinMessage);
	}

	private void OnServerRoomStart(NetworkConnection conn, RoomStartRequest msg)
	{
		RoomStartResponse response = new RoomStartResponse();
		
		int roomCode;
		if (!m_PlayersInRoom.TryGetValue(conn.connectionId, out roomCode))
		{
			conn.Send(response);
			return;
		}

		Room room = m_Rooms[roomCode];
		if (!room.IsHost(conn) || room.OnGoing)
		{
			conn.Send(response);
			return;
		}

		SceneManager.LoadScene(
			"SampleScene", // TODO: Temporary name, in the future, server will generate a game room based on game mode
			new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.Physics3D });

		room.SetScene(SceneManager.GetSceneAt(m_NumLoadedScenes));
		// Spawner.InitialSpawn(newScene);

		++m_NumLoadedScenes;

		response.success = true;
		conn.Send(response);

		foreach (NetworkConnection c in room.Clients)
			if (c.connectionId != room.Host.connectionId)
				c.Send(new RoomStartMessage());
	}

	public override void OnServerAddPlayer(NetworkConnection conn) 
	{
		int roomCode;
		if (!m_PlayersInRoom.TryGetValue(conn.connectionId, out roomCode))
			return; // Player not in room

		PlayerInfo info;
		if (!TryGetPlayerInfoFromConnection(conn, out info))
			return; // Unknown error... player not authenticated?

		StartCoroutine(ServerAddPlayerToRoom(conn, info, m_Rooms[roomCode]));
	}

	IEnumerator ServerAddPlayerToRoom(NetworkConnection conn, PlayerInfo playerInfo, Room room)
	{
		conn.Send(new SceneMessage { sceneName = room.GameScene.name, sceneOperation = SceneOperation.Normal });

		// NOTE: Wait for end of frame before adding the player to ensure Scene Message goes first 
		// and that the scene has been loaded on the server.
		//
		yield return new WaitForEndOfFrame();

		// NOTE: Since player is a NetworkIdentity, it will start disabled
		//		 providing to opportunity to initialize it further.
		//
		Transform startPos = GetStartPosition();
		GameObject playerGO = startPos != null
			? Instantiate(playerPrefab, startPos.position, startPos.rotation)
			: Instantiate(playerPrefab);

		Player player = playerGO.GetComponent<Player>();
		player.m_PlayerInitInfo = playerInfo;

		player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
		NetworkServer.AddPlayerForConnection(conn, playerGO);

		SceneManager.MoveGameObjectToScene(conn.identity.gameObject, room.GameScene);
	}

	private bool TryGetPlayerInfoFromConnection(NetworkConnection conn, out PlayerInfo info)
	{
		string playerID = conn.authenticationData as string;

		info = new PlayerInfo();
		if (string.IsNullOrEmpty(playerID))
			return false;

		return DumbDatabase.TryGetPlayerInfo(playerID, out info);
	}

	// Client Build
	public delegate void DelegateRoomNewUserJoin(RoomNewUserJoinMessage msg);
	public DelegateRoomNewUserJoin onRoomNewUserJoinCallback;

	private string username;

	public override void OnStartClient()
	{
		base.OnStartClient();

		NetworkClient.RegisterHandler<LoginResponse>(OnLogin);
		NetworkClient.RegisterHandler<RoomJoinCreateResponse>(OnRoomJoinCreate);
		NetworkClient.RegisterHandler<RoomNewUserJoinMessage>(OnRoomNewUserJoinCallback);
		NetworkClient.RegisterHandler<RoomStartResponse>(OnRoomStartHost);
		NetworkClient.RegisterHandler<RoomStartMessage>(OnRoomStart);
	}

	public override void OnClientConnect(NetworkConnection conn)
	{
		LoginRequest msg = new LoginRequest();
		msg.username = username;
		NetworkClient.connection.Send(msg);
	}

	public void OnGUI()
	{
		GUILayout.BeginArea(new Rect(Screen.width - 225, 40, 215, 9999));
		GUILayout.Label("username:");
		username = GUILayout.TextField(username);
		GUILayout.EndArea();
	}

	public void OnLogin(LoginResponse msg)
	{
		if (!msg.success)
		{
			Debug.LogError("Could not login to the server!");
			return;
		}

		Debug.Log("Login successful!");
	}

	public void OnRoomJoinCreate(RoomJoinCreateResponse msg)
	{
		if (!msg.success)
		{
			Debug.LogError("Failed to join/create room for unknown reasons");
			return;
		}

		StartCoroutine(LoadNextSceneAsync(1, msg));
	}

	IEnumerator LoadNextSceneAsync(int buildIndex, RoomJoinCreateResponse msg)
	{
		AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(buildIndex);

		while (!asyncLoad.isDone)
			yield return null;

		LobbyUI lobbyUI = GameObject.Find("Canvas").GetComponent<LobbyUI>();
		lobbyUI.Init(msg.roomInfo);
	}

	public void OnRoomStartHost(RoomStartResponse msg)
	{
		if (!msg.success)
		{
			Debug.LogError("Failed to start room for unknown reasons");
			return;
		}

		NetworkClient.Ready();
		NetworkClient.AddPlayer();
	}

	public void OnRoomStart(RoomStartMessage msg)
	{
		NetworkClient.Ready();
		NetworkClient.AddPlayer();
	}

	public void OnRoomNewUserJoinCallback(RoomNewUserJoinMessage msg)
	{
		onRoomNewUserJoinCallback?.Invoke(msg);
	}
}