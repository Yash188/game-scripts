using Game.Networking;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Managers
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager mInstance;

        private NetworkClient Client;


        void Awake()
        {
            if (mInstance == null)
            {
                mInstance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(this);
            }
        }

        private void Start()
        {
            Client = NetworkClient.GetInstance;
            Client.onJoinedServiceRoom += onJoinedServiceRoom;
            Client.onJoinedPlayingRoom += onJoinedPlayingRoom;
            Client.onJoinedPrivateroom += onJoinedPrivateRoom;
            Client.onRoomLeaved += OnLeaveRoom;
            Client.OnLogout += OnLogout;
            Client.OnLeaveServiceRoom += OnLogout;
        }
         

        private void onJoinedServiceRoom()
        {
            Debug.Log("Joined Service Room");
            SceneManager.LoadScene("Main Menu", LoadSceneMode.Single);

        }


        private void onJoinedPlayingRoom()
        {
            Debug.Log("Joined Playing Room");

            SceneManager.LoadScene("Public Room", LoadSceneMode.Single);

        }

        private void onJoinedPrivateRoom()
        {
            Debug.Log("Joined Private Room");

            SceneManager.LoadScene("Private Room");

        }

        private void OnLeaveRoom()
        {
            Debug.Log("Leave Room");
            //SceneManager.LoadScene();
            SceneManager.LoadScene("Main Menu", LoadSceneMode.Single);

        }

        private void OnLogout()
        {
            Debug.Log("Logging Out");
            SceneManager.LoadScene("LogIn Scene");
        }

         

    }
}
