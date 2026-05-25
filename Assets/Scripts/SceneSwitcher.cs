using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour, IPointerClickHandler
{
    public string targetScene;

    public void OnPointerClick(PointerEventData eventData)
    {
        SceneManager.LoadScene(targetScene);
    }
}
