using UnityEngine;
    
public class StandPoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Host"))
        {
            Debug.Log("Entered StandPoint");
            var host = other.GetComponent<Host>();
            if (host)
            {
                host.TurnAndGo(); // 让Host停顿、掉头、再移动
            }
        }
    }
}