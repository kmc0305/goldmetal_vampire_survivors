using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public enum Infotype { Exp, Level, Kill, Time, Health }
    public Infotype type;

    Text myText;
    Slider mySlider;

    void Awake()
    {
        myText = GetComponent<Text>();
        mySlider = GetComponent<Slider>();
    }

    void LateUpdate()
    {
        var gm = GameManager.instance;
        if (gm == null) return;

        switch (type)
        {
            case Infotype.Exp:
                if (mySlider == null) return;

                // level 인덱스 보호 (0은 0, 1이상은 배열 끝을 넘지 않게)
                int levelIndex = Mathf.Clamp(gm.level, 1, gm.nextExp.Length - 1);
                float curExp = gm.exp;
                float maxExp = gm.nextExp[levelIndex];

                float ratio = (maxExp > 0f) ? (curExp / maxExp) : 0f;
                mySlider.value = ratio;
                break;

            case Infotype.Level:
                if (myText == null) return;
                myText.text = string.Format("Lv.{0:000}", gm.level);
                break;

            case Infotype.Kill:
                if (myText == null) return;
                myText.text = string.Format("{0}", gm.kill);
                break;

            case Infotype.Time:
                float remainTime = gm.maxGameTime - gm.gameTime;
                int min = Mathf.FloorToInt(remainTime / 60);
                int sec = Mathf.FloorToInt(remainTime % 60);
                myText.text = string.Format("{0:D2}:{1:D2}", min, sec);
                break;

            case Infotype.Health:
                if (mySlider == null) return;
                float curHealth = gm.health;
                float maxHealth = gm.maxHealth;
                mySlider.value = (maxHealth > 0f) ? (curHealth / maxHealth) : 0f;
                break;
        }
    }
}
