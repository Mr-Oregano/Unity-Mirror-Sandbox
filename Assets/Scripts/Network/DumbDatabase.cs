using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DumbDatabase 
{
	private static Dictionary<string, PlayerInfo> s_Users = new Dictionary<string, PlayerInfo>()
	{
		{ "gay", new PlayerInfo{ name = "cheese", color = Color.green } },
		{ "jim", new PlayerInfo{ name = "cracker", color = Color.red } }
	};

	public static UserInfoResponse GetPlayerInfo(string id)
	{
		UserInfoResponse response = new UserInfoResponse();

		PlayerInfo info;
		if (!s_Users.TryGetValue(id, out info))
			return response;

		response.info = info;
		response.success = true;
		return response;
	}

	public class UserInfoResponse
	{
		public bool success;
		public PlayerInfo info;
	}

}
