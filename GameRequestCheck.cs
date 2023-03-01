using PlayerIOClient;
using System; 
using UnityEngine; 
using TMPro;
using Game.Utility;
using Game.UI;

namespace Game.Networking
{
    public class GameRequestCheck : MonoBehaviour
    {
        Client client;
        [SerializeField]Transform requestPanelTransform;
        [SerializeField]TMP_Text requestTxt;

        private string roomId;
        private int tableAmount;

        private void Start()
        {
            InvokeRepeating("UpdateRequestCheck", 0f,1f);
        }

        private void UpdateRequestCheck()
        {
            client = NetworkClient.GetInstance.client;

            client.GameRequests.Refresh(delegate ()
            { 

                /// Go over all requests
                foreach (GameRequest request in client.GameRequests.WaitingRequests)
                {
                    if (request.Type == "invite")
                    { 
                        /// I'll only show request if it was sent less then one minute before
                        if (DateTime.UtcNow.CompareTo(request.Created.AddMinutes(1f)) < 0)
                        { 
                            roomId = request.Data["roomId"];
                            tableAmount = TableInfo.GetTableAmountFromCategory(request.Data["table_category"]);

                            requestTxt.text = request.Data["user_name"] +" Invited you to play for " + request.Data["table_category"];
                            requestPanelTransform.gameObject.SetActive(true);
                        } 
                    }
                    else
                    {
                        //Handle other requests...
                    }

                    /// Delete the request after handling it
                    client.GameRequests.Delete(client.GameRequests.WaitingRequests,() => Debug.Log("successfully deleted"));
                }
                 
            }, delegate (PlayerIOError playerIOError)
             {
                 Debug.Log("Refresh Error : " + playerIOError.Message);
             });
        }

        public void GotoRoom()
        {
            /// we'll check first if the player has sufficient balance 
            /// if not, then we'll show insufficient balance panel

            client.BigDB.LoadMyPlayerObject((DatabaseObject playerObj) =>
            {
                float balance = playerObj.GetFloat(NetworkConstant.DATABASE_COINS_CONSTANT);

                if(balance > tableAmount)
                {
                    NetworkClient.GetInstance.GotoFriendsRoom(roomId);
                }
                else
                {
                    // show insufficient balance panel
                    MainMenuScript.mInstance.DisplayBuyCoinsPanel();
                    CloseRequestPanel(); 
                } 
            }); 
        }

        public void CloseRequestPanel()
        {
            requestPanelTransform.gameObject.SetActive(false);
        }
    }
}
