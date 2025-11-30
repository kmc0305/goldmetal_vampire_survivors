using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class DescPanel : MonoBehaviour
{
    Text info;
    GameObject thisPanel;

    private void Awake()
    {
        info=GetComponentsInChildren<Text>()[2];
        info.text = "Lv. _";//Init.
        thisPanel = GetComponent<GameObject>();
        thisPanel.SetActive(false);
    }
    private void txtchange()
    {

    }
}
