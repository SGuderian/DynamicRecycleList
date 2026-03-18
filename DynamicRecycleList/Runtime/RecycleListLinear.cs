using System;
using UnityEngine;

namespace DynamicRecycleList
{
    public class RecycleListLinear : RecycleListCore
    {
        [Header("Layout")]
        [SerializeField] private Direction _direction = Direction.Vertical;
        [SerializeField] private Vector2 spacing;
        [SerializeField] private RectOffset padding;
        [SerializeField] private Vector2 defaultItemSize = new Vector2(100, 100);

        public override Direction direction => _direction;

        public Func<int, Vector2> onGetItemSize;

        protected override Vector2 GetItemSize(int index)
        {
            return onGetItemSize?.Invoke(index) ?? defaultItemSize;
        }

        protected override float CalculateLayout(float[] sizes, float[] outStartPositions)
        {
            float currentPos = direction == Direction.Vertical ? padding.top : padding.left;
        
            for (int i = 0; i < sizes.Length; i++)
            {
                outStartPositions[i] = currentPos;
                currentPos += sizes[i];
            
                if (i < sizes.Length - 1)
                {
                    currentPos += direction == Direction.Vertical ? spacing.y : spacing.x;
                }
            }
        
            float totalSize = currentPos;
            if (direction == Direction.Vertical)
            {
                totalSize += padding.bottom;
            }
            else
            {
                totalSize += padding.right;
            }
        
            return totalSize;
        }

        protected override Vector2 GetItemLocalPosition(int index, float majorSize)
        {
            float majorPos = itemStartPositions[index];
            float minorPos = direction == Direction.Vertical ? padding.left : padding.top;
        
            if (direction == Direction.Vertical)
            {
                return new Vector2(minorPos, -majorPos);
            }
            else
            {
                return new Vector2(majorPos, -minorPos);
            }
        }
    }
}
