using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;

public class Button_lvlup : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    public GameObject wpn;
    public int w_level=0;
    public Sprite iconimg;

    Image icon;
    Text textlevel;
    public GameObject connectedPanel;

    public void OnPointerEnter(PointerEventData eventData)
    {
        connectedPanel.SetActive(true);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        connectedPanel.SetActive(false);
    }
    void Awake()
    {
        icon=GetComponentsInChildren<Image>()[1];
        icon.sprite = iconimg;
        textlevel= GetComponentInChildren<Text>();
        connectedPanel.GetComponentsInChildren<Text>()[2].text = "Lvl. " + w_level;
    }

    void LateUpdate()
    {
        textlevel.text = "LV. " + w_level;
        TurnOnOff();
    }
    public void OnClick()
    {
        foreach (Component c in wpn.GetComponents<Component>())
        {
            if (c is Weapon)
            {
                c.GetComponent<Weapon>().LevelUp(w_level+1);
            }
            else if (c is RangeWeapon)
            {
                c.GetComponent<RangeWeapon>().LevelUp(w_level+1);
            }
            else if (c is BombardWeapon)
            {
                c.GetComponent<BombardWeapon>().LevelUp(w_level+1);
            }
        }
        w_level++;
        connectedPanel.GetComponentsInChildren<Text>()[2].text = "Lvl. "+w_level;
        GameManager.instance.points--;

        if (w_level >= 5)   //무기의 만렙은 5을 기본으로 한다.(0레벨은 무기 미사용)
        {
            ColorBlock cb = GetComponent<Button>().colors;
            cb.normalColor = Color.gray;
            GetComponent<Button>().colors = cb;
            GetComponent<Button>().interactable = false;
        }
    }

    void TurnOnOff()
    {
        if (GameManager.instance.points <= 0 || w_level >= 5) 
            GetComponent<Button>().interactable = false;
        else
            GetComponent<Button>().interactable = true;
    }
}
