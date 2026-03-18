
namespace DynamicRecycleList
{
    /// <summary>Item生命周期接口</summary>
    public interface IRecycleListItem
    {
        void OnAppear();    // 出现时调用
        void OnDisappear(); // 消失时调用
        void OnRecycle();   // 回收时调用
        void OnUpdate(int index); // 更新数据时调用
    }
}