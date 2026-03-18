using DynamicRecycleList;
using UnityEngine;
using UnityEngine.UI;

public class Demo : MonoBehaviour
{
    public RecycleListLinear list_v;
    public RecycleListLinear list_h;
    public int totalCount = 1000;

    void Start()
    {
        list_v.onGetItemSize = (index) => new Vector2(300, index % 2 == 0 ? 100 : 200);
        list_v.onUpdateItem = (go, idx) => {
            Text text = go.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = "Item " + idx;
            }
        };
        list_v.SetTotalCount(totalCount);
        
        list_h.onGetItemSize = (index) => new Vector2(index % 2 == 0 ? 100 : 200, 100);
        list_h.onUpdateItem = (go, idx) => {
            Text text = go.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = "Item " + idx;
            }
        };
        list_h.SetTotalCount(totalCount);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            list_v.ScrollToIndex(5, 0, true);
        }
    }
}