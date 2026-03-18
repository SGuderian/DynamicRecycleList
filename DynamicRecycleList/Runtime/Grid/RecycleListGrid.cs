using System;
using UnityEngine;

namespace DynamicRecycleList
{
    public class RecycleListGrid : RecycleListCore
    {
        [Header("Grid Layout")] [SerializeField]
        private Direction _direction = Direction.Vertical;

        [SerializeField] private Vector2 spacing;
        [SerializeField] private RectOffset padding;
        [SerializeField] private int columnCount = 3;
        [SerializeField] private Vector2 defaultItemSize = new Vector2(100, 100);

        public override Direction direction => _direction;

        public Func<int, Vector2> onGetItemSize;

        protected override Vector2 GetItemSize(int index)
        {
            return onGetItemSize?.Invoke(index) ?? defaultItemSize;
        }

        protected override float CalculateLayout(float[] sizes, float[] outStartPositions)
        {
            if (direction == Direction.Vertical)
            {
                return CalculateVerticalLayout(sizes, outStartPositions);
            }
            else
            {
                return CalculateHorizontalLayout(sizes, outStartPositions);
            }
        }

        private float CalculateVerticalLayout(float[] sizes, float[] outStartPositions)
        {
            float currentY = padding.top;
            float rowHeight = 0;
            int itemCountInRow = 0;

            for (int i = 0; i < sizes.Length; i++)
            {
                if (itemCountInRow == 0)
                {
                    currentY += rowHeight;
                    if (i > 0) currentY += spacing.y;
                    rowHeight = 0;
                }

                outStartPositions[i] = currentY;
                rowHeight = Mathf.Max(rowHeight, sizes[i]);
                itemCountInRow++;

                if (itemCountInRow >= columnCount)
                {
                    itemCountInRow = 0;
                }
            }

            float totalHeight = currentY + rowHeight + padding.bottom;
            return totalHeight;
        }

        private float CalculateHorizontalLayout(float[] sizes, float[] outStartPositions)
        {
            float currentX = padding.left;
            float columnWidth = 0;
            int itemCountInColumn = 0;

            for (int i = 0; i < sizes.Length; i++)
            {
                if (itemCountInColumn == 0)
                {
                    currentX += columnWidth;
                    if (i > 0) currentX += spacing.x;
                    columnWidth = 0;
                }

                outStartPositions[i] = currentX;
                columnWidth = Mathf.Max(columnWidth, sizes[i]);
                itemCountInColumn++;

                if (itemCountInColumn >= columnCount)
                {
                    itemCountInColumn = 0;
                }
            }

            float totalWidth = currentX + columnWidth + padding.right;
            return totalWidth;
        }

        protected override Vector2 GetItemLocalPosition(int index, float majorSize)
        {
            if (direction == Direction.Vertical)
            {
                int row = index / columnCount;
                int column = index % columnCount;

                float rowY = itemStartPositions[index];
                float columnWidth = (viewportMinor - padding.left - padding.right - (columnCount - 1) * spacing.x) /
                                    columnCount;

                float x = padding.left + column * (columnWidth + spacing.x);
                float y = -rowY;

                return new Vector2(x, y);
            }
            else
            {
                int column = index / columnCount;
                int row = index % columnCount;

                float columnX = itemStartPositions[index];
                float rowHeight = (viewportMinor - padding.top - padding.bottom - (columnCount - 1) * spacing.y) /
                                  columnCount;

                float x = columnX;
                float y = -(padding.top + row * (rowHeight + spacing.y));

                return new Vector2(x, y);
            }
        }
    }
}