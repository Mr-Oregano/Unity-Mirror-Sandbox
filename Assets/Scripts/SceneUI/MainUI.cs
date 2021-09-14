
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System;

public class MainUI : MonoBehaviour
{
	[SerializeField]
	private InputField m_RoomCodeField;

	public void JoinBtnClick()
	{
		string roomCode = m_RoomCodeField.text;

		if (string.IsNullOrEmpty(roomCode))
		{
			Debug.LogError("Room Code field must have a value");
			return;
		}

		RoomJoinRequest msg = new RoomJoinRequest();
		msg.roomCode = Int32.Parse(roomCode); // NOTE: Should be safe since input field only accepts integers.
		NetworkClient.Send(msg);
	}

	public void HostBtnClick()
	{
		NetworkClient.Send(new RoomCreateRequest());
	}
}
