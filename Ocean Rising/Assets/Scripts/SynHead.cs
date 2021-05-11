﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SynHead : MonoBehaviour
{
    public Camera VRcam;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float camY = VRcam.transform.eulerAngles.y;
        float camX = VRcam.transform.eulerAngles.x;
        float camZ = VRcam.transform.eulerAngles.z;
        float posY = VRcam.transform.position.y;
        float posX = VRcam.transform.position.x;
        float posZ = VRcam.transform.position.z;
        transform.eulerAngles = new Vector3(camX,camY,camZ);
        transform.position = new Vector3(posX,posY,posZ);
        
    }
}