using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Valve.VR;

public class PauseMulti : MonoBehaviour
{
    [SerializeField] public SteamVR_Action_Boolean menuButton;
    public GameObject player;
    public GameObject pausedPlayer;
    public Camera camera;
    public static bool isGamePaused = false;
    public GameObject pauseMenu;
    public float m_DefaultLength = 5.0f;
    // public GameObject m_Dot;
    // public VRInput m_InputModule;
    // private LineRenderer m_LineRenderer = null;

    // Update is called once per frame
    void Start()
    {
        // playingPlayer.SetActive(false);
        // m_Dot.SetActive(false);
        player.SetActive(true);
        pauseMenu.SetActive(false);
        pausedPlayer.SetActive(false);
    }
    void Update()
    {
        bool menuButtonPressed = menuButton.state;
        if(menuButtonPressed)
        {
            PauseGame();
            // UpdateLine();
        }
    }


    public void ResumeGame()
    {
        pauseMenu.SetActive(false);
        pausedPlayer.SetActive(false);
        player.SetActive(true);
        isGamePaused = false;
        Debug.Log("Game has resumed");
        // m_Dot.SetActive(false);
    }
    public void PauseGame()
    {
        player.SetActive(false);
        pauseMenu.SetActive(true);
        pausedPlayer.SetActive(true);
        isGamePaused = true;
        Debug.Log("Game is Paused");
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
        Debug.Log("Menu has been loaded");
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit");
    }

    //POINTER
    
    // private void Awake()
    // {
    //     m_LineRenderer = GetComponent<LineRenderer>();
    // }
    // private void UpdateLine()
    // {
    //     // m_Dot.SetActive(true);
    //     PointerEventData data = m_InputModule.GetData();
    //     float targetLength = data.pointerCurrentRaycast.distance == 0 ? m_DefaultLength : data.pointerCurrentRaycast.distance;
    //     RaycastHit hit = CreateRaycast(targetLength);
    //     Vector3 endPosition = transform.position + (transform.forward * targetLength);
    // if(hit.collider != null)
    //     endPosition = hit.point;

    //     // m_Dot.transform.position = endPosition;

    //     m_LineRenderer.SetPosition(0, transform.position);
    //     m_LineRenderer.SetPosition(1, endPosition);
    // }

    // private RaycastHit CreateRaycast(float length)
    // {
    //     RaycastHit hit;
    //     Ray ray = new Ray(transform.position, transform.forward);
    //     Physics.Raycast(ray, out hit, m_DefaultLength);

    //     return hit;
    // }
}
