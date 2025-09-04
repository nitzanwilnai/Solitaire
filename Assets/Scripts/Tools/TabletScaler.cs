using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TabletScaler : MonoBehaviour {

	public float scale = 0.5f;

	// Use this for initialization
	void Start () {
		if (IsWideDevice())
			transform.localScale = Vector3.one * scale;
	}

	public static bool IsWideDevice()
	{
		float screenRatio = (float)Screen.width / (float)Screen.height;
		return (screenRatio > 0.6f);
	}
}
