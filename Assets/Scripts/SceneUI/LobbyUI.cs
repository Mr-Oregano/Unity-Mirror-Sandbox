
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class LobbyUI : MonoBehaviour
{
	[SerializeField]
	private Text m_RoomCodeLbl;

	[SerializeField]
	private Button m_StartMatchBtn;

	private RoomInfo m_RoomInfo;

	void Start()
	{
		GameNetworkManager netManager = NetworkManager.singleton as GameNetworkManager;
		netManager.onRoomNewUserJoinCallback += OnUserJoin; 
	}

	public void Init(RoomInfo roomInfo)
	{
		m_RoomInfo = roomInfo;
		m_RoomCodeLbl.text += roomInfo.code;

		m_RoomCodeLbl.gameObject.SetActive(true);
		if (roomInfo.isLocalPlayerHost)
			m_StartMatchBtn.gameObject.SetActive(true);
	}

	public void StartMatchBtnClick()
	{
		NetworkClient.Send(new RoomStartRequest());
	}

	public void OnUserJoin(RoomNewUserJoinMessage msg)
	{
		Debug.Log(msg.newPlayer.name + " has joined the room!");
	}
}
