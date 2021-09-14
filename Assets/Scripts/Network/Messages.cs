using Mirror;

public struct LoginRequest : NetworkMessage
{
	// NOTE: This is obviously not how you're supposed to do this
	//		 it's just here as an example but in general you'd have
	//		 some form of credentials that the server will verify.
	//
	public string username;
	public string password;
}
public struct LoginResponse : NetworkMessage
{
	public bool success;
}

public struct RoomCreateRequest : NetworkMessage { }
public struct RoomJoinRequest : NetworkMessage
{
	public int roomCode;
}
public struct RoomJoinCreateResponse : NetworkMessage
{
	public bool success;
	public RoomInfo roomInfo;
}

public struct RoomStartRequest : NetworkMessage { }
public struct RoomStartResponse : NetworkMessage
{
	public bool success;
}

public struct RoomNewUserJoinMessage : NetworkMessage
{
	public PlayerInfo newPlayer;
}
public struct RoomStartMessage : NetworkMessage { }
