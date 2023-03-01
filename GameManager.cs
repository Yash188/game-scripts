 
using Game.Gameplay.Player;
using Game.Gameplay.Table;
using Game.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine; 
using PlayerIOClient;
using Game.Utility; 
using Game.UI;
using Game.UI.Panels;

namespace Game.Gameplay.Managers
{
    public class GameManager : MonoBehaviour
    {
        //singleton instance
        public static GameManager mInstance;

        [SerializeField] GameObject[] playerPrefabs; 
        [SerializeField] Transform networkContainer;
        [SerializeField] GameStatus gameStatus;
        [SerializeField] DeckPicker PickerInstance;
        [SerializeField] ProgressBar progressBar;

        public NetworkIdentity currentplayerIdentity { get; private set; }
        public bool IsSecondPhaseStarted { get; private set; } = false;


        private NetworkClient Client;
        private List<NetworkIdentity> identities;
        private Table.Table table;
        private TimeManager timeManager;
        private bool isGameStarted = false;
        private List<GameObject> logicalPlayerArrangementArray;
        private List<int> logicalTableSpawnPos;

        #region mono overrides
        private void Awake()
        {
            if (mInstance == null)
            {
                mInstance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void Start()
        {
            table = FindObjectOfType<Table.Table>();
            timeManager = GetComponent<TimeManager>();
            identities = new List<NetworkIdentity>();

            Client = NetworkClient.GetInstance;
            Client.onPlayerSpawned += OnPlayerSpawned;
            Client.OnPlayerLeft += OnPlayerLeft;
            Client.OnStartTimer += OnStartTimer;
            Client.OnStopTimer += OnStopTimer;
            Client.OnGameStarted += OnGameStarted;
            Client.OnTurnChanged += OnTurnChanged;
            Client.OnCardThrowResult += OnCardThrowResult;
            Client.OnCardPenalty += OnCardPenalty;
            Client.OnGameNotificationReceived += OnGameNotificationReceived;

            Client.OnSecondPhaseStarted += OnSecondPhaseStarted;
            Client.OnServerSpawned += OnServerSpawn;
            Client.OnRoundOver += OnRoundOver;
            Client.OnPlayerWon += OnPlayerWon;
            Client.OnGameOver += OnGameOver;
            Client.OnResetGame += OnResetGame;
            Client.OnChatMessageReceived += OnChatMessageReceived;
            Client.OnGameTie += OnGameTie;

        }

        private void OnDestroy()
        {

            Client.onPlayerSpawned -= OnPlayerSpawned;
            Client.OnPlayerLeft -= OnPlayerLeft;
            Client.OnStartTimer -= OnStartTimer;
            Client.OnStopTimer -= OnStopTimer;
            Client.OnGameStarted -= OnGameStarted;
            Client.OnTurnChanged -= OnTurnChanged;
            Client.OnCardThrowResult -= OnCardThrowResult;
            Client.OnCardPenalty -= OnCardPenalty;
            Client.OnGameNotificationReceived -= OnGameNotificationReceived;

            Client.OnSecondPhaseStarted -= OnSecondPhaseStarted;
            Client.OnServerSpawned -= OnServerSpawn;
            Client.OnRoundOver -= OnRoundOver;
            Client.OnPlayerWon -= OnPlayerWon;
            Client.OnGameOver -= OnGameOver;
            Client.OnResetGame -= OnResetGame;
            Client.OnChatMessageReceived -= OnChatMessageReceived;
            Client.OnGameTie -= OnGameTie;
        }

        void OnApplicationQuit()
        {

            Client.DisconnectAll();
        }
        #endregion

        #region game start callbacks
        private void OnGameNotificationReceived(string _msg)
        {
            NotificationsPanel.mInstance.SetNotification(_msg);
        }

        private void OnStartTimer()
        {
            Debug.Log("On Start Timer");
            ShowGameStatus("Game is Starting");
        }

        private void OnStopTimer()
        {
            Debug.Log("On Start Timer");
            if (identities.Count == 1)
            {
                ShowGameStatus("Waiting for players.....");
            }
        }

        private void OnPlayerSpawned(Message message)
        {

            InstantiatePrefab(message);
            if (identities.Count == 1)
            {
                ShowGameStatus("Waiting for players.....");
            }
            else
            {
                ShowGameStatus("Game is Starting");
            }

            NotificationsPanel.mInstance.SetNotification(message.GetString(1) + " joined the table");
        }

        private void OnGameStarted()
        {
            Debug.Log("Game Started");
            hideGameStatus();
            PickerInstance.gameObject.SetActive(true);
            progressBar.gameObject.SetActive(true);
            isGameStarted = true;
        }
        #endregion

        private void OnTurnChanged(string playerID,bool isActive,string _receivedColor)
        {
            ///informing all the player managers about the turn change
            foreach (NetworkIdentity identity in identities)
            { 
               identity.GetComponent<PlayerManager>().IsMyTurn(playerID, IsSecondPhaseStarted, _receivedColor);  
            }

            table.IS_MY_TURN = Client.client.ConnectUserId == playerID;

            if(isActive)
                timeManager.StartTimer(Constants.TIME_BETWEEN_TURNS);
            else
                timeManager.StartTimer(Constants.INACTIVE_PLAYER_TURN_TIME);

            
        }

        #region first phase specific methods

        /// <summary>
        /// called in first phase when somone throws a card or 
        /// when one run's out of timer server automatically picks a card from my card list
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="card"></param>
        private void OnCardThrowResult(Message message)
        {

            ///getting card thrown by a player(local/foreign)
            int transfer_code = 0;
            string playerID = message.GetString(0);
            string card = message.GetString(1); 
            string currPlayerID = message.GetString(message.Count - 2);

/*            Debug.Log("Result : "+playerID+","+card+","+currPlayerID+","+message.GetString(message.Count-3));*/

            /// if local player thrown card
            if (Client.GetID.Equals(currPlayerID))
            {
                foreach (NetworkIdentity identity in identities)
                {
                    if (identity.GetID().Equals(playerID))
                    {
                        PickerInstance.AdjustCardLocation(card, identity.GetComponent<PlayerManager>().GetPlayerDropper, identity);
                        return;
                    }
                }
                
                return;
            }

            /// if someone else thrown 
            /// checking if its transferred
            if(message.Count > 3)
            transfer_code = message.GetInt(message.Count - 1);

            ///destroy any duplicate card
            DestroyAnyDuplicateCard(card);

            /// finding player manager to whom the card goes and telling to accept card
            foreach (NetworkIdentity identity in identities)
            {
                if (identity.GetID().Equals(playerID))
                {
                    if(transfer_code == NetworkConstant.DIRECTLY_FROM_TABLE)
                    {
                        identity.GetComponent<PlayerManager>().AcceptCard(card, PickerInstance.transform.position, transfer_code);

                    }else if(transfer_code == NetworkConstant.DIRECT_CARD_TRANSFER)
                    {
                        string prevOwner = message.GetString(3);
                        identity.GetComponent<PlayerManager>().AcceptCard(card, GetPlayerPositionFromID(prevOwner)
                            ,transfer_code);
                    }


                    timeManager.StartTimer(Constants.TIME_BETWEEN_TURNS);
                    SoundManager.mInstance.PlayTransferSound();
                    return;

                }
            }
             
        }

        /// <summary>
        /// called when player forgots to put card on other
        /// params received {0->card receiver 1-> card giver 2-> cardface}
        /// </summary>
        /// <param name="_msg"></param>
        private void OnCardPenalty(Message _msg)
        {
            string _receiver = _msg.GetString(0);
            string _giver = _msg.GetString(1);
            string _cardFace = _msg.GetString(2);


            foreach (NetworkIdentity identity in identities)
            {
                if (identity.GetID().Equals(_receiver))
                {
                    DestroyAnyDuplicateCard(_cardFace); 
                    identity.GetComponent<PlayerManager>().AcceptCard(_cardFace, GetPlayerPositionFromID(_giver),
                        NetworkConstant.PENALTY_CARD_TRANSFER,true);
                    break;
                }
            }
        }

        #endregion


        private void OnChatMessageReceived(string playerID,string msgContent)
        {
            foreach(NetworkIdentity nI in identities)
            {
                if (playerID.Equals(nI.GetID()))
                {
                    nI.GetComponent<PlayerManager>().DisplayMessage(msgContent);
                }
            }
        }


        #region second phase specific methods

        private void OnSecondPhaseStarted(Message message)
        {
            Debug.Log("Second Phase Started");
            IsSecondPhaseStarted = true;
            table.IS_SECOND_PHASE_STARTED = true;

            //destroying the phase one cards
            foreach (NetworkIdentity identity in identities)
            {
                identity.GetComponent<PlayerManager>().DestroyCards();
            }

            currentplayerIdentity.GetComponent<PlayerManager>().SpawnCards(message);

            ///we'll just deactivate the deck picker obj
            PickerInstance.gameObject.SetActive(false);
        }

        /// <summary>
        /// called when foreign player throws a card on the table
        /// </summary>
        /// <param name="playerID"></param>
        /// <param name="cardName"></param>
        private void OnServerSpawn(string playerID, string cardName)
        {
            table.ServerSpawnCard(playerID, cardName); 
        }

        /// <summary>
        /// called when a round is over
        /// </summary>
        /// <param name="message"></param>
        private void OnRoundOver(Message message)
        {
            Debug.Log("On Round Over");
            table.OnRoundOver(message);
        }

        #endregion

        #region game lifecycle methods


        private void OnPlayerLeft(string _playerID)
        {
            Debug.Log("Player Left : "+_playerID);
             foreach(NetworkIdentity nId in identities)
            {
                if (nId.GetID().Equals(_playerID))
                {
                    nId.gameObject.SetActive(false);
                    NotificationsPanel.mInstance.SetNotification(nId.Username+" left the table");
                    identities.Remove(nId);

                    /// check if the game hasnt started 
                    /// and if unsufficient players the update game status
                    if (!isGameStarted && identities.Count == 1)
                    {
                        ShowGameStatus("Waiting for players...");
                    }

                    return;
                }
            }

           

        }

        private void OnPlayerWon(string _playerID,string _coinsWon)
        {
             
            if (_playerID.Equals(Client.GetID))
            {
                ShowGameStatus("You've Won : "+_coinsWon+ " Chips");
                NotificationsPanel.mInstance.SetNotification("You've Won : " + _coinsWon + " Chips");

                if (PlayerPrefs.GetString(Constants.RATE_SHOW_PREF, "").Equals(""))
                {
                    PlayerPrefs.SetString(Constants.RATE_SHOW_PREF, "yes");
                }


            }
            else
            {
                foreach(NetworkIdentity nI in identities)
                {
                    if (nI.GetID().Equals(_playerID) && !_playerID.Equals(Client.GetID))
                    {
                        nI.GetComponent<PlayerManager>().PlayerWon();
                        NotificationsPanel.mInstance.SetNotification(nI.Username + " has won "+_coinsWon+ " Chips");
                    }
                }
            }

        }

        private void OnGameOver(string playerID)
        {
            Debug.Log("Game over Player lost " + playerID);
            hideGameStatus();
            if (Client.GetID.Equals(playerID))
            {
                ShowGameStatus("You Lost!");
                GetComponent<PlayingGameSceneUI>().CloseGameScreenCountdown();
            }
            else
            {
                ShowGameStatus("You've Won");
                NotificationsPanel.mInstance.SetNotification("Game Over!");
                GetComponent<PlayingGameSceneUI>().CloseGameScreenCountdown();
            }

            /// pausing progress bar
            ProgressBar.mInstance.PauseProgress();
            
        }
        

        private void OnGameTie()
        {
            ShowGameStatus("Game Tied");
            StartCoroutine(ShowGameOverScreen());
        }

        IEnumerator ShowGameOverScreen()
        {
            yield return new WaitForSeconds(3);
            OnGameOver("");
        }

        private void OnResetGame()
        {
            Debug.Log("Resetting game");

            ///inform all player managers
            foreach (NetworkIdentity identity in identities)
            {
                identity.GetComponent<PlayerManager>().ResetPlayerPrefab();
            }

            ///add the deck picker and do all necessary changes
            IsSecondPhaseStarted = false;
            table.IS_SECOND_PHASE_STARTED = false;
        }

        #endregion 
          

        #region private custom methods

        private void InstantiatePrefab(Message msg)
        {
           
            string userID = msg.GetString(0);
            string username = msg.GetString(1);
            int coins = msg.GetInt(2);

            Texture2D profileTex = new Texture2D(128,128);
            profileTex.LoadImage(msg.GetByteArray(3));

 
            /// if the player already exists ill not spawn again
            /// 

            if (networkContainer == null)
                return;

            Transform nI = networkContainer.Find(userID); 
            if(nI != null)
            {
                return;
            }

            GameObject game;

            /// logic of proper player spawning 
            if (msg.Type.Equals(NetworkConstant.SPAWN_MYSELF))
            {
                game = playerPrefabs[0];

                ArrangePlayerAccordingly(msg.GetInt(4));
            }
            else
            {
                game = logicalPlayerArrangementArray[identities.Count-1];
            }


            NetworkIdentity ni = game.GetComponent<NetworkIdentity>();
            ni.SetControllerID(userID);
            ni.ProfileImage = profileTex;
            ni.Username = username;
            ni.Coins = coins;
             
            game.name = userID;
            game.transform.localScale = Vector3.one;
            if (ni.IsControlling()) { currentplayerIdentity = ni; }
            identities.Add(ni);
            game.SetActive(true);

            if (msg.Type.Equals(NetworkConstant.SPAWN_MYSELF))
            {
                ni.TableSpawnPos = 0;
            }
            else
            {
                ni.TableSpawnPos = logicalTableSpawnPos[identities.Count - 2];
            }
        }

        private void ArrangePlayerAccordingly(int _myPosition)
        {
            logicalPlayerArrangementArray = new List<GameObject>();
            logicalTableSpawnPos = new List<int>();

            switch (_myPosition)
            {
                case 1:
                    logicalPlayerArrangementArray.Add(playerPrefabs[1]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[2]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[3]);

                    logicalTableSpawnPos.Add(1);
                    logicalTableSpawnPos.Add(2);
                    logicalTableSpawnPos.Add(3);
                    break;
                case 2:
                    logicalPlayerArrangementArray.Add(playerPrefabs[3]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[1]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[2]);

                    logicalTableSpawnPos.Add(3);
                    logicalTableSpawnPos.Add(1);
                    logicalTableSpawnPos.Add(2);
                    break;
                case 3:
                    logicalPlayerArrangementArray.Add(playerPrefabs[2]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[3]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[1]);

                    logicalTableSpawnPos.Add(2);
                    logicalTableSpawnPos.Add(3);
                    logicalTableSpawnPos.Add(1);
                    break;
                case 4:
                    logicalPlayerArrangementArray.Add(playerPrefabs[1]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[2]);
                    logicalPlayerArrangementArray.Add(playerPrefabs[3]);

                    logicalTableSpawnPos.Add(1);
                    logicalTableSpawnPos.Add(2);
                    logicalTableSpawnPos.Add(3);
                    break;
            }
        }

        private void DestroyAnyDuplicateCard(string card)
        {
            Transform[] pos = networkContainer.GetComponentsInChildren<Transform>();
            foreach (Transform t in pos)
            {
                if (t.name.Equals(card))
                {
                    Destroy(t.gameObject);
                }
            }
        }

        public Vector3 GetPlayerPositionFromID(string _playerID)
        {
            Vector3 playerPos = Vector3.zero;

            foreach(NetworkIdentity identity in identities)
            {
                if (identity.GetID().Equals(_playerID))
                {
                    playerPos =  identity.GetComponent<PlayerManager>().GetPlayerDropperPosition;
                }
            }

            return playerPos;
        }

        private void ShowGameStatus(string _msg)
        {
            gameStatus.SetStatus(_msg);
            gameStatus.gameObject.SetActive(true);
        }

        private void hideGameStatus()
        {
            gameStatus.gameObject.SetActive(false);
        }

        #endregion


        #region public api's/methods

        public int GetIndexFromPlayerID(string _playerID)
        { 
            foreach(NetworkIdentity _ni in identities)
            {
                if (_ni.GetID().Equals(_playerID))
                { 
                    return _ni.TableSpawnPos;
                } 
            }

            return 0;
        }

        #endregion
    }
}
