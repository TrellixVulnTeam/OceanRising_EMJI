﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitch : MonoBehaviour
{
    public string NewLevel= "Main";
    void OnTriggerEnter(Collider other)
    {
        SceneManager.LoadScene(NewLevel);
    }
}