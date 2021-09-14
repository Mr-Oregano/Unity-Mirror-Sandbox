
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

		// TODO: Here is where you validate the credentials
		//		 For now we just use simple hardcoded ifs
		//
		if (msg.username == "cheese" && msg.password == "password1")
		{
			conn.authenticationData = "gay";
			response.success = true;
		}
		else if (msg.username == "cracker" && msg.password == "password2")
		{
			conn.authenticationData = "gay";
			response.success = true;
		}

		conn.Send(response);
	}

	private void OnServerRoomCreate(NetworkConnection conn, RoomCreateRequest msg)
	{
		RoomJoinCreateResponse response = new RoomJoinCreateResponse();

		DumbDatabase.UserInfoResponse user;
		if (!TryGetPlayerInfo(conn, out user) || !user.success)
		{
			conn.Send(response);
			return;
		}

		// TODO: Generate a random unique room code.
		int newRoomCode = Random.Range(0, 99999);

		Room newRoom = new Room(conn);

		newRoom.m_Info.code = newRoomCode;
		newRoom.m_Info.playerInfos = new List<PlayerInfo>();
		newRoom.m_Info.playerInfos.Add(user.info);
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

		DumbDatabase.UserInfoResponse user;
		Room room;
		if (!TryGetPlayerInfo(conn, out user) || !user.success || !m_Rooms.TryGetValue(msg.roomCode, out room))
		{
			conn.Send(response);
			return;
		}

		room.AddClientConnection(conn);
		m_PlayersInRoom.Add(conn.connectionId, room.m_Info.code);

		room.m_Info.playerInfos.Add(user.info);
		room.m_Info.isLocalPlayerHost = false;

		response.roomInfo = room.m_Info;
		response.success = true;
		conn.Send(response);

		// NOTE: Send the update to all of the players in the room
		RoomNewUserJoinMessage userJoinMessage = new RoomNewUserJoinMessage();
		userJoinMessage.newPlayer = user.info;

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

		response.success = true;
		conn.Send(response);

		StartCoroutine(ServerLoadGameRoom(room, "SampleScene"));
		
		foreach (NetworkConnection c in room.Clients)
			if (c.connectionId != conn.connectionId)
				c.Send(new RoomStartMessage());
	}

	public override void OnServerAddPlayer(NetworkConnection conn) 
	{
		int roomCode;
		if (!m_PlayersInRoom.TryGetValue(conn.connectionId, out roomCode))
			return; // Player not in room

		DumbDatabase.UserInfoResponse user;
		if (!TryGetPlayerInfo(conn, out user) || !user.success)
			return;

		StartCoroutine(ServerAddPlayerToRoom(conn, user.info, m_Rooms[roomCode]));
	}

	IEnumerator ServerAddPlayerToRoom(NetworkConnection conn, PlayerInfo playerInfo, Room room)
	{
		while (!room.OnGoing); // Wait for room to load on server

		conn.Send(new SceneMessage { sceneName = room.GameScene.name, sceneOperation = SceneOperation.Normal });

		// Wait for end of frame before adding the player to ensure Scene Message goes first
		yield return new WaitForEndOfFrame();

		// NOTE: Since player is a NetworkIdentity, it will start disabled
		//		 providing to opportunity to initialize it further.
		//
		Transform startPos = GetStartPosition();
		GameObject playerGO = startPos != null
			? Instantiate(playerPrefab, startPos.position, startPos.rotation)
			: Instantiate(playerPrefab);

		Player player = playerGO.GetComponent<Player>();
		player.CreateOnServer(playerInfo);

		player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
		NetworkServer.AddPlayerForConnection(conn, playerGO);

		SceneManager.MoveGameObjectToScene(conn.identity.gameObject, room.GameScene);
		
		// NOTE: Currently sending the player info to all clients once
		//		 at the start of the, instead the client should probably
		//		 request the info from the server or something idk
		//
		player.CreateRPC(playerInfo);
	}

	IEnumerator ServerLoadGameRoom(Room room, string sceneName)
	{
		yield return SceneManager.LoadSceneAsync(
			sceneName, 
			new LoadSceneParameters { loadSceneMode = LoadSceneMode.Additive, localPhysicsMode = LocalPhysicsMode.Physics3D });

		room.SetScene(SceneManager.GetSceneAt(m_NumLoadedScenes));
		// Spawner.InitialSpawn(newScene);

		++m_NumLoadedScenes;
	}

	private bool TryGetPlayerInfo(NetworkConnection conn, out DumbDatabase.UserInfoResponse info)
	{
		string playerID = conn.authenticationData as string;

		if (string.IsNullOrEmpty(playerID))
		{
			// NOTE: The player is not logged in
			info = new DumbDatabase.UserInfoResponse();
			return false;
		}

		info = DumbDatabase.GetPlayerInfo(playerID);
		return true;
	}

	// Client Build
	public delegate void DelegateRoomNewUserJoin(RoomNewUserJoinMessage msg);
	public DelegateRoomNewUserJoin onRoomNewUserJoinCallback;

	[SerializeField]
	private string username;

	[SerializeField]
	private string password;

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
		msg.password = password;
		NetworkClient.connection.Send(msg);
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