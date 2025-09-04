using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NotchOffset : MonoBehaviour {

	public float topOffset = 100.0f;

	// Use this for initialization
	void Start () {
		if (IsTallDevice())
        {
            Vector3 position = transform.localPosition;
			position.y -= topOffset;
			transform.localPosition = position;
        }
	}

	public static bool IsTallDevice()
	{
		float screenRatio = (float)Screen.width / (float)Screen.height;
		return (screenRatio < 0.56f); // 9/16 is 0.5625, iPhone 7/8 is slightly lower
	}
}
