
using UnityEngine;
using Mirror;

public class Player : NetworkBehaviour
{
	[SerializeField]
	private float m_Speed = 5f;

	[Server]
	public void CreateOnServer(PlayerInfo info)
	{
		Debug.Log(info.name + "'s player object created");
		GetComponent<MeshRenderer>().materials[0].color = info.color;
	}

	[ClientRpc]
	public void CreateRPC(PlayerInfo info)
	{
		Debug.Log(info.name + "'s player object created");
		GetComponent<MeshRenderer>().materials[0].color = info.color;
	}

	public override void OnStartLocalPlayer()
	{
		Camera.main.transform.parent = transform;
		Camera.main.transform.position += new Vector3(0, 0, -10f);
	}

	private void Update()
	{
		if (!isLocalPlayer)
			return;

		Vector3 velocity = Vector2.zero;

		if (Input.GetKey(KeyCode.A))
			--velocity.x;
		if (Input.GetKey(KeyCode.D))
			++velocity.x;
		if (Input.GetKey(KeyCode.S))
			--velocity.y;
		if (Input.GetKey(KeyCode.W))
			++velocity.y;

		transform.position += velocity.normalized * Time.deltaTime * m_Speed;
	}
}
