using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DumbDatabase 
{
	private static Dictionary<string, PlayerInfo> s_Users = new Dictionary<string, PlayerInfo>()
	{
		{ "gay", new PlayerInfo{ name = "cheese", color = Color.yellow } },
		{ "sam", new PlayerInfo{ name = "grapes", color = Color.magenta } },
		{ "tam", new PlayerInfo{ name = "salt", color = Color.white } },
		{ "jim", new PlayerInfo{ name = "salad", color = Color.green } }
	};

	public static bool TryGetPlayerInfo(string id, out PlayerInfo info)
	{
		if (!s_Users.TryGetValue(id, out info))
			return false;

		return true;
	}
}
