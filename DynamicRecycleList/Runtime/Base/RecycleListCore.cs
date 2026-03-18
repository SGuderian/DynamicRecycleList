using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DynamicRecycleList
{
    /// <summary>
    /// 循环列表核心
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class RecycleListCore : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("References")] [SerializeField]
        protected RectTransform viewport;

        [SerializeField] protected RectTransform content;
        [SerializeField] protected GameObject defaultItemPrefab;

        [Header("Scroll Settings")] [SerializeField]
        protected float scrollSensitivity = 1f;

        [SerializeField] protected bool inertia = true;
        [SerializeField] protected float decelerationRate = 0.135f;

        [Header("Callbacks")] public Action<GameObject, int> onUpdateItem;
        public Func<int, GameObject> onGetItemPrefab;

        // 对象池
        protected Dictionary<string, Stack<GameObject>> typePools = new Dictionary<string, Stack<GameObject>>();
        protected List<RecycleItemData> activeItems = new List<RecycleItemData>();

        // 滚动状态
        protected float scrollOffset;
        protected float contentMajorSize;
        protected bool isDragging;
        protected Vector2 lastPointerPosition;
        protected float velocity;
        protected float smoothScrollTarget;
        protected bool isSmoothScrolling;

        // 布局缓存
        protected int totalCount;
        protected float[] itemSizes;
        protected float[] itemStartPositions;
        protected int visibleStartIndex = -1;
        protected int visibleEndIndex = -1;

        protected float viewportMajor => direction == Direction.Vertical ? viewport.rect.height : viewport.rect.width;
        protected float viewportMinor => direction == Direction.Vertical ? viewport.rect.width : viewport.rect.height;

        public abstract Direction direction { get; }

        protected virtual void Awake()
        {
            InitializeContent();
        }

        protected virtual void InitializeContent()
        {
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = viewport.rect.size;
            content.anchoredPosition = Vector2.zero;
        }

        public virtual void SetTotalCount(int count)
        {
            totalCount = Mathf.Max(0, count);
            RebuildLayout();
        }

        public virtual void RebuildLayout()
        {
            ClearAllItems();

            if (totalCount == 0)
            {
                contentMajorSize = 0;
                UpdateContentSize();
                return;
            }

            itemSizes = new float[totalCount];
            for (int i = 0; i < totalCount; i++)
            {
                Vector2 size = GetItemSize(i);
                itemSizes[i] = direction == Direction.Vertical ? size.y : size.x;
            }

            itemStartPositions = new float[totalCount];
            contentMajorSize = CalculateLayout(itemSizes, itemStartPositions);

            UpdateContentSize();
            scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, contentMajorSize - viewportMajor));
            UpdateVisibleItems();
        }

        protected virtual void UpdateContentSize()
        {
            if (direction == Direction.Vertical)
            {
                content.sizeDelta = new Vector2(viewport.rect.width, Mathf.Max(contentMajorSize, viewportMajor));
            }
            else
            {
                content.sizeDelta = new Vector2(Mathf.Max(contentMajorSize, viewportMajor), viewport.rect.height);
            }
        }

        public virtual void RefreshItem(int index)
        {
            if (index < 0 || index >= totalCount) return;

            float oldSize = itemSizes[index];
            Vector2 newSize = GetItemSize(index);
            float newMajorSize = direction == Direction.Vertical ? newSize.y : newSize.x;

            if (!Mathf.Approximately(oldSize, newMajorSize))
            {
                float delta = newMajorSize - oldSize;
                itemSizes[index] = newMajorSize;

                for (int i = index + 1; i < totalCount; i++)
                {
                    itemStartPositions[i] += delta;
                }

                contentMajorSize += delta;
                UpdateContentSize();

                foreach (var itemData in activeItems)
                {
                    if (itemData.index > index)
                    {
                        UpdateItemPosition(itemData);
                    }
                    else if (itemData.index == index)
                    {
                        UpdateItemSizeAndPosition(itemData, newMajorSize);
                    }
                }

                scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, contentMajorSize - viewportMajor));
            }

            ForceUpdateItem(index);
        }

        public virtual void ForceUpdateItem(int index)
        {
            foreach (var itemData in activeItems)
            {
                if (itemData.index == index)
                {
                    onUpdateItem?.Invoke(itemData.gameObject, index);
                    var itemComp = itemData.gameObject.GetComponent<IRecycleListItem>();
                    itemComp?.OnUpdate(index);
                    break;
                }
            }
        }

        protected abstract Vector2 GetItemSize(int index);
        protected abstract float CalculateLayout(float[] sizes, float[] outStartPositions);
        protected abstract Vector2 GetItemLocalPosition(int index, float majorSize);

        public virtual void SetScrollOffset(float offset)
        {
            float maxOffset = Mathf.Max(0, contentMajorSize - viewportMajor);
            offset = Mathf.Clamp(offset, 0, maxOffset);

            if (Mathf.Approximately(scrollOffset, offset)) return;

            scrollOffset = offset;
            UpdateVisibleItems();
        }

        public float GetScrollOffset() => scrollOffset;
        public float GetContentMajorSize() => contentMajorSize;

        #region 滚动交互

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isSmoothScrolling) return;

            isDragging = true;
            velocity = 0;
            lastPointerPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || isSmoothScrolling) return;

            Vector2 delta = eventData.delta;
            float moveDelta = direction == Direction.Vertical ? delta.y : delta.x;

            if (direction == Direction.Vertical)
            {
                scrollOffset += moveDelta * scrollSensitivity;
            }
            else
            {
                scrollOffset -= moveDelta * scrollSensitivity;
            }

            scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, contentMajorSize - viewportMajor));

            UpdateVisibleItems();
            lastPointerPosition = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;

            if (inertia)
            {
                Vector2 delta = eventData.position - lastPointerPosition;
                velocity = direction == Direction.Vertical ? delta.y : delta.x;
                velocity /= Time.deltaTime;
                velocity *= scrollSensitivity;
            }
        }

        protected virtual void Update()
        {
            if (isSmoothScrolling)
            {
                float diff = smoothScrollTarget - scrollOffset;
                if (Mathf.Abs(diff) < 1f)
                {
                    scrollOffset = smoothScrollTarget;
                    isSmoothScrolling = false;
                    UpdateVisibleItems();
                }
                else
                {
                    scrollOffset += diff * 8f * Time.deltaTime;
                    scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, contentMajorSize - viewportMajor));
                    UpdateVisibleItems();
                }

                return;
            }

            if (inertia && !isDragging && Mathf.Abs(velocity) > 0.1f)
            {
                scrollOffset += velocity * Time.deltaTime;
                scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, contentMajorSize - viewportMajor));

                velocity *= Mathf.Pow(decelerationRate, Time.deltaTime * 60f);
                if (Mathf.Abs(velocity) < 0.1f) velocity = 0;

                UpdateVisibleItems();
            }
        }

        #endregion

        #region 可视项管理

        protected virtual void UpdateVisibleItems()
        {
            if (totalCount == 0)
            {
                ClearAllItems();
                return;
            }

            float viewStart = scrollOffset;
            float viewEnd = scrollOffset + viewportMajor;

            int newStartIndex = FindFirstVisibleIndex(viewStart, viewEnd);
            int newEndIndex = FindLastVisibleIndex(viewStart, viewEnd);

            if (newStartIndex == -1 || newEndIndex == -1)
            {
                ClearAllItems();
                visibleStartIndex = -1;
                visibleEndIndex = -1;
                return;
            }

            visibleStartIndex = newStartIndex;
            visibleEndIndex = newEndIndex;

            HashSet<int> neededIndices = new HashSet<int>();
            for (int i = newStartIndex; i <= newEndIndex; i++)
            {
                neededIndices.Add(i);
            }

            List<RecycleItemData> toRecycle = new List<RecycleItemData>();
            foreach (var itemData in activeItems)
            {
                if (!neededIndices.Contains(itemData.index))
                {
                    toRecycle.Add(itemData);
                }
            }

            foreach (var itemData in toRecycle)
            {
                RecycleItem(itemData);
            }

            for (int i = newStartIndex; i <= newEndIndex; i++)
            {
                if (!HasItemAtIndex(i))
                {
                    CreateItemAtIndex(i);
                }
            }

            UpdateAllActiveItemsPosition();
        }

        protected virtual void UpdateAllActiveItemsPosition()
        {
            foreach (var itemData in activeItems)
            {
                UpdateItemPosition(itemData);
            }
        }

        protected virtual void UpdateItemPosition(RecycleItemData itemData)
        {
            Vector2 localPos = GetItemLocalPosition(itemData.index, itemSizes[itemData.index]);
            Vector2 finalPos = localPos;

            if (direction == Direction.Vertical)
            {
                finalPos.y += scrollOffset;
            }
            else
            {
                finalPos.x -= scrollOffset;
            }

            itemData.rectTransform.anchoredPosition = finalPos;
        }

        protected virtual void UpdateItemSizeAndPosition(RecycleItemData itemData, float newMajorSize)
        {
            if (direction == Direction.Vertical)
            {
                itemData.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newMajorSize);
            }
            else
            {
                itemData.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newMajorSize);
            }

            UpdateItemPosition(itemData);
        }

        protected virtual bool HasItemAtIndex(int index)
        {
            foreach (var itemData in activeItems)
            {
                if (itemData.index == index) return true;
            }

            return false;
        }

        protected virtual void CreateItemAtIndex(int index)
        {
            GameObject prefab = onGetItemPrefab?.Invoke(index) ?? defaultItemPrefab;
            string prefabName = prefab.name;

            if (!typePools.ContainsKey(prefabName))
            {
                typePools[prefabName] = new Stack<GameObject>();
            }

            GameObject go;
            if (typePools[prefabName].Count > 0)
            {
                go = typePools[prefabName].Pop();
            }
            else
            {
                go = Instantiate(prefab, content);
                go.name = prefabName;
            }

            go.SetActive(true);
            RectTransform rt = go.GetComponent<RectTransform>();

            RecycleItemData itemData = new RecycleItemData
            {
                gameObject = go,
                rectTransform = rt,
                index = index
            };

            activeItems.Add(itemData);
            rt.anchorMax = Vector2.up;
            rt.anchorMin = Vector2.up;
            rt.pivot = Vector2.up;
            if (direction == Direction.Vertical)
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemSizes[index]);
            }
            else
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, itemSizes[index]);
            }

            UpdateItemPosition(itemData);

            onUpdateItem?.Invoke(go, index);
            var itemComp = go.GetComponent<IRecycleListItem>();
            itemComp?.OnAppear();
            itemComp?.OnUpdate(index);
        }

        protected virtual void RecycleItem(RecycleItemData itemData)
        {
            if (itemData == null) return;

            itemData.gameObject.SetActive(false);
            activeItems.Remove(itemData);

            var itemComp = itemData.gameObject.GetComponent<IRecycleListItem>();
            itemComp?.OnDisappear();
            itemComp?.OnRecycle();

            string prefabName = itemData.gameObject.name;
            if (!typePools.ContainsKey(prefabName))
            {
                typePools[prefabName] = new Stack<GameObject>();
            }

            typePools[prefabName].Push(itemData.gameObject);
        }

        protected virtual void ClearAllItems()
        {
            for (int i = activeItems.Count - 1; i >= 0; i--)
            {
                RecycleItem(activeItems[i]);
            }

            activeItems.Clear();
            visibleStartIndex = -1;
            visibleEndIndex = -1;
        }

        private int FindFirstVisibleIndex(float viewStart, float viewEnd)
        {
            int left = 0, right = totalCount - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                float itemEnd = itemStartPositions[mid] + itemSizes[mid];

                if (itemEnd > viewStart)
                {
                    result = mid;
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            return result;
        }

        private int FindLastVisibleIndex(float viewStart, float viewEnd)
        {
            int left = 0, right = totalCount - 1;
            int result = -1;

            while (left <= right)
            {
                int mid = (left + right) / 2;

                if (itemStartPositions[mid] < viewEnd)
                {
                    result = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return result;
        }

        #endregion

        public virtual void ScrollToIndex(int index, float offset = 0, bool smooth = false)
        {
            if (index < 0 || index >= totalCount) return;

            float targetOffset = itemStartPositions[index] + offset;
            targetOffset = Mathf.Clamp(targetOffset, 0, Mathf.Max(0, contentMajorSize - viewportMajor));

            if (smooth)
            {
                smoothScrollTarget = targetOffset;
                isSmoothScrolling = true;
            }
            else
            {
                SetScrollOffset(targetOffset);
            }
        }

        public bool IsIndexVisible(int index)
        {
            if (index < 0 || index >= totalCount) return false;
            return index >= visibleStartIndex && index <= visibleEndIndex;
        }

        public Rect GetItemRect(int index)
        {
            if (index < 0 || index >= totalCount) return new Rect();

            float start = itemStartPositions[index] - scrollOffset;
            float size = itemSizes[index];

            if (direction == Direction.Vertical)
            {
                return new Rect(0, -start - size, viewportMinor, size);
            }
            else
            {
                return new Rect(start, 0, size, viewportMinor);
            }
        }

        protected class RecycleItemData
        {
            public GameObject gameObject;
            public RectTransform rectTransform;
            public int index;
        }
    }
}

