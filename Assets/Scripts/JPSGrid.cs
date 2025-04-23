using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

// 定义方向枚举，表示8个可能的移动方向
public enum eDirections
{
    NORTH = 0,      // 北
    NORTH_EAST = 1, // 东北
    EAST = 2,       // 东
    SOUTH_EAST = 3, // 东南
    SOUTH = 4,      // 南
    SOUTH_WEST = 5, // 西南
    WEST = 6,       // 西
    NORTH_WEST = 7, // 西北
}

// 节点类，表示网格中的一个位置
public class Node
{
    public Point pos;                    // 节点的位置坐标
    public bool isObstacle = false;      // 是否为障碍物
    public int[] jpDistances = new int[8]; // 到各个方向跳点的距离
    public bool isJumpPoint = false;     // 是否为跳点

    // 记录从各个方向到达该节点时是否为跳点
    public bool[] jumpPointDirection = new bool[8];

    // 判断从指定方向到达该节点时是否为跳点
    public bool isJumpPointComingFrom(eDirections dir)
    {
        // 如果节点是跳点且在指定方向上有标记，则返回true
        return this.isJumpPoint && this.jumpPointDirection[(int)dir];
    }
}

// 路径查找返回结果类
public class PathfindReturn
{
    // 路径查找状态枚举
    public enum PathfindStatus
    {
        SEARCHING,  // 正在搜索
        FOUND,      // 找到路径
        NOT_FOUND   // 未找到路径
    }

    public PathfindingNode _current;     // 当前节点
    public PathfindStatus _status = PathfindStatus.SEARCHING; // 当前状态
    public List<Point> path = new List<Point>(); // 找到的路径
}

// 点结构体，表示二维坐标
public struct Point : IEquatable<Point>
{
    public int column, row; // 列和行坐标

    public Point(int row, int column)
    {
        this.row = row;
        this.column = column;
    }

    // 判断两个点是否相等
    public bool Equals(Point other)
    {
        return this.column == other.column && this.row == other.row;
    }

    // 计算两个点之间的差异（假设只能进行正交或对角线移动）
    public static int diff(Point a, Point b)
    {
        int diff_columns = Mathf.Abs(b.column - a.column);
        int diff_rows = Mathf.Abs(b.row - a.row);
        return Mathf.Max(diff_rows, diff_columns);
    }

    public override string ToString()
    {
        return "(" + this.column + "," + this.row + ")";
    }
}

// 节点在列表中的状态
public enum ListStatus
{
    ON_NONE,    // 不在任何列表中
    ON_OPEN,    // 在开放列表中
    ON_CLOSED   // 在关闭列表中
}

// 路径查找节点类
public class PathfindingNode
{
    public PathfindingNode parent;       // 父节点
    public Point pos;                    // 位置坐标
    public int givenCost;                // 从起点到当前节点的实际代价
    public int finalCost;                // 总代价（实际代价 + 启发式代价）
    public eDirections directionFromParent; // 从父节点到达当前节点的方向
    public ListStatus listStatus = ListStatus.ON_NONE; // 当前状态

    // 重置节点状态
    public void Reset()
    {
        // 将父节点引用设为null
        this.parent = null;
        // 重置从起点到当前节点的实际代价
        this.givenCost = 0;
        // 重置总代价（实际代价 + 启发式代价）
        this.finalCost = 0;
        // 重置节点状态为不在任何列表中
        this.listStatus = ListStatus.ON_NONE;
    }
}

// 网格类，实现JPS算法的核心功能
public class Grid
{
    public Node[] gridNodes = new Node[0];           // 网格节点数组
    public PathfindingNode[] pathfindingNodes = new PathfindingNode[0]; // 路径查找节点数组

    // 定义每个方向的有效移动方向查找表
    private Dictionary<eDirections, eDirections[]> validDirLookUpTable = new Dictionary<eDirections, eDirections[]>
    {
        {
            eDirections.SOUTH,
            new[]
            {
                eDirections.WEST, eDirections.SOUTH_WEST, eDirections.SOUTH, eDirections.SOUTH_EAST, eDirections.EAST
            }
        },
        { eDirections.SOUTH_EAST, new[] { eDirections.SOUTH, eDirections.SOUTH_EAST, eDirections.EAST } },
        {
            eDirections.EAST,
            new[]
            {
                eDirections.SOUTH, eDirections.SOUTH_EAST, eDirections.EAST, eDirections.NORTH_EAST, eDirections.NORTH
            }
        },
        { eDirections.NORTH_EAST, new[] { eDirections.EAST, eDirections.NORTH_EAST, eDirections.NORTH } },
        {
            eDirections.NORTH,
            new[]
            {
                eDirections.EAST, eDirections.NORTH_EAST, eDirections.NORTH, eDirections.NORTH_WEST, eDirections.WEST
            }
        },
        { eDirections.NORTH_WEST, new[] { eDirections.NORTH, eDirections.NORTH_WEST, eDirections.WEST } },
        {
            eDirections.WEST,
            new[]
            {
                eDirections.NORTH, eDirections.NORTH_WEST, eDirections.WEST, eDirections.SOUTH_WEST, eDirections.SOUTH
            }
        },
        { eDirections.SOUTH_WEST, new[] { eDirections.WEST, eDirections.SOUTH_WEST, eDirections.SOUTH } }
    };

    private eDirections[] allDirections = Enum.GetValues(typeof(eDirections)).Cast<eDirections>().ToArray();

    // 获取最大行数
    private int maxRows
    {
        get { return gridNodes.Length / rowSize; }
    }

    // 将方向转换为字符串
    static string dirToStr(eDirections dir)
    {
        // 根据方向枚举返回对应的字符串表示
        switch (dir)
        {
            case eDirections.NORTH:
                return "NORTH";
            case eDirections.NORTH_EAST:
                return "NORTH_EAST";
            case eDirections.EAST:
                return "EAST";
            case eDirections.SOUTH_EAST:
                return "SOUTH_EAST";
            case eDirections.SOUTH:
                return "SOUTH";
            case eDirections.SOUTH_WEST:
                return "SOUTH_WEST";
            case eDirections.WEST:
                return "WEST";
            case eDirections.NORTH_WEST:
                return "NORTH_WEST";
        }

        // 如果不是有效的方向，返回"NONE"
        return "NONE";
    }

    public int rowSize = 0; // 每行的节点数量

    // 获取东北方向节点的索引
    private int getNorthEastIndex(int row, int column)
    {
        // 检查是否在边缘位置，如果是则返回-1表示无效
        if (column + 1 >= rowSize || row - 1 < 0) return -1;

        // 计算东北方向节点的索引：向右一列，向上一行
        return (column + 1) +
               (row - 1) * rowSize;
    }

    // 获取东南方向节点的索引
    private int getSouthEastIndex(int row, int column)
    {
        // 检查是否在边缘位置，如果是则返回-1表示无效
        if (column + 1 >= rowSize || row + 1 >= maxRows) return -1;

        // 计算东南方向节点的索引：向右一列，向下一行
        return (column + 1) +
               (row + 1) * rowSize;
    }

    // 获取西南方向节点的索引
    private int getSouthWestIndex(int row, int column)
    {
        // 检查是否在边缘位置，如果是则返回-1表示无效
        if (column - 1 < 0 || row + 1 >= maxRows) return -1;

        // 计算西南方向节点的索引：向左一列，向下一行
        return (column - 1) +
               (row + 1) * rowSize;
    }

    // 获取西北方向节点的索引
    private int getNorthWestIndex(int row, int column)
    {
        // 检查是否在边缘位置，如果是则返回-1表示无效
        if (column - 1 < 0 || row - 1 < 0) return -1;

        // 计算西北方向节点的索引：向左一列，向上一行
        return (column - 1) +
               (row - 1) * rowSize;
    }

    // 将行列转换为数组索引
    private int rowColumnToIndex(int row, int column)
    {
        // 计算在一维数组中的索引位置
        return column + (row * rowSize);
    }

    // 将点转换为数组索引
    private int pointToIndex(Point pos)
    {
        // 调用rowColumnToIndex方法将点的行列坐标转换为数组索引
        return rowColumnToIndex(pos.row, pos.column);
    }

    // 判断指定索引的节点是否为空
    private bool isEmpty(int index)
    {
        // 如果索引无效，返回false
        if (index < 0) return false;

        // 计算行列坐标
        int row, column;
        row = index / rowSize;
        column = index % rowSize;

        // 调用对应的行列判断方法
        return isEmpty(row, column);
    }

    // 判断指定索引的节点是否为障碍物或墙
    private bool isObstacleOrWall(int index)
    {
        // 如果索引无效，则视为障碍物或墙
        if (index < 0) return true;

        // 计算行列坐标
        int row, column;
        row = index / rowSize;
        column = index % rowSize;

        // 调用对应的行列判断方法
        return isObstacleOrWall(row, column);
    }

    // 判断指定行列的节点是否为空
    private bool isEmpty(int row, int column)
    {
        // 不是障碍物或墙就是空的
        return !isObstacleOrWall(row, column);
    }

    // 判断指定行列的节点是否为障碍物或墙
    private bool isObstacleOrWall(int row, int column)
    {
        // 如果超出边界，则视为墙
        // 如果在边界内，则检查对应的节点是否为障碍物
        return isInBounds(row, column) && gridNodes[column + (row * rowSize)].isObstacle;
    }

    // 判断指定行列的节点在指定方向是否为跳点
    private bool isJumpPoint(int row, int column, eDirections dir)
    {
        // 检查是否在边界内
        if (isInBounds(row, column))
        {
            // 获取对应的节点
            Node node = gridNodes[column + (row * rowSize)];
            // 判断是否为跳点以及在指定方向上是否标记为跳点
            return node.isJumpPoint && node.jumpPointDirection[(int)dir];
        }

        // 如果超出边界，则不是跳点
        return false;
    }

    // 判断索引是否在有效范围内
    private bool isInBounds(int index)
    {
        // 检查索引是否小于0或超出网格大小
        if (index < 0 || index >= gridNodes.Length) return false;

        // 计算行列坐标
        int row, column;
        row = index / rowSize;
        column = index % rowSize;

        // 调用对应的行列边界检查方法
        return isInBounds(row, column);
    }

    // 判断行列是否在有效范围内
    private bool isInBounds(int row, int column)
    {
        // 检查行是否在0到最大行数之间，列是否在0到每行大小之间
        return row >= 0 && row < maxRows && column >= 0 && column < rowSize;
    }

    // 获取指定方向上的节点索引
    private int getIndexOfNodeTowardsDirection(int index, eDirections direction)
    {
        // 计算当前节点的行列坐标
        int row, column;
        row = index / rowSize;
        column = index % rowSize;

        // 根据方向确定行和列的变化值
        int change_row = 0;
        int change_column = 0;

        // 确定行方向的变化
        switch (direction)
        {
            case eDirections.NORTH_EAST:
            case eDirections.NORTH:
            case eDirections.NORTH_WEST:
                // 北向移动，行减少
                change_row = -1;
                break;

            case eDirections.SOUTH_EAST:
            case eDirections.SOUTH:
            case eDirections.SOUTH_WEST:
                // 南向移动，行增加
                change_row = 1;
                break;
        }

        // 确定列方向的变化
        switch (direction)
        {
            case eDirections.NORTH_EAST:
            case eDirections.EAST:
            case eDirections.SOUTH_EAST:
                // 东向移动，列增加
                change_column = 1;
                break;

            case eDirections.SOUTH_WEST:
            case eDirections.WEST:
            case eDirections.NORTH_WEST:
                // 西向移动，列减少
                change_column = -1;
                break;
        }

        // 计算新的行列坐标
        int new_row = row + change_row;
        int new_column = column + change_column;

        // 检查新坐标是否在地图范围内
        if (isInBounds(new_row, new_column))
        {
            // 返回新坐标对应的索引
            return new_column + (new_row * rowSize);
        }

        // 如果超出范围，返回-1表示无效
        return -1;
    }

    // 构建主要跳点
    public void buildPrimaryJumpPoints()
    {
        // 遍历所有节点，识别强制邻居情况下的跳点
        for (int i = 0; i < gridNodes.Length; ++i)
        {
            Node current_node = gridNodes[i];

            // 只有障碍物周围才会产生强制邻居（跳点）
            if (current_node.isObstacle)
            {
                // 计算当前障碍物的行列坐标
                int row, column;
                row = i / rowSize;
                column = i % rowSize;

                // 检查四个对角线方向的节点是否为跳点
                int north_east_index, south_east_node, south_west_node, north_west_node;

                // 检查东北方向节点
                north_east_index = getNorthEastIndex(row, column);

                if (north_east_index != -1)
                {
                    Node node = gridNodes[north_east_index];

                    // 如果东北方向的节点不是障碍物
                    if (!node.isObstacle)
                    {
                        // 检查它的南方和西方是否都不是障碍物
                        // 如果都不是障碍物，则该节点成为从南方和西方来时的跳点
                        if (isEmpty(getIndexOfNodeTowardsDirection(north_east_index, eDirections.SOUTH)) &&
                            isEmpty(getIndexOfNodeTowardsDirection(north_east_index, eDirections.WEST)))
                        {
                            node.isJumpPoint = true;
                            // 标记该节点是从南方来时的跳点
                            node.jumpPointDirection[(int)eDirections.SOUTH] = true;
                            // 标记该节点是从西方来时的跳点
                            node.jumpPointDirection[(int)eDirections.WEST] = true;
                        }
                    }
                }

                // 检查东南方向节点
                south_east_node = getSouthEastIndex(row, column);

                if (south_east_node != -1)
                {
                    Node node = gridNodes[south_east_node];

                    // 如果东南方向的节点不是障碍物
                    if (!node.isObstacle)
                    {
                        // 检查它的北方和西方是否都不是障碍物
                        // 如果都不是障碍物，则该节点成为从北方和西方来时的跳点
                        if (isEmpty(getIndexOfNodeTowardsDirection(south_east_node, eDirections.NORTH)) &&
                            isEmpty(getIndexOfNodeTowardsDirection(south_east_node, eDirections.WEST)))
                        {
                            node.isJumpPoint = true;
                            // 标记该节点是从北方来时的跳点
                            node.jumpPointDirection[(int)eDirections.NORTH] = true;
                            // 标记该节点是从西方来时的跳点
                            node.jumpPointDirection[(int)eDirections.WEST] = true;
                        }
                    }
                }

                // 检查西南方向节点
                south_west_node = getSouthWestIndex(row, column);

                if (south_west_node != -1)
                {
                    Node node = gridNodes[south_west_node];

                    // 如果西南方向的节点不是障碍物
                    if (!node.isObstacle)
                    {
                        // 检查它的北方和东方是否都不是障碍物
                        // 如果都不是障碍物，则该节点成为从北方和东方来时的跳点
                        if (isEmpty(getIndexOfNodeTowardsDirection(south_west_node, eDirections.NORTH)) &&
                            isEmpty(getIndexOfNodeTowardsDirection(south_west_node, eDirections.EAST)))
                        {
                            node.isJumpPoint = true;
                            // 标记该节点是从北方来时的跳点
                            node.jumpPointDirection[(int)eDirections.NORTH] = true;
                            // 标记该节点是从东方来时的跳点
                            node.jumpPointDirection[(int)eDirections.EAST] = true;
                        }
                    }
                }

                // 检查西北方向节点
                north_west_node = getNorthWestIndex(row, column);

                if (north_west_node != -1)
                {
                    Node node = gridNodes[north_west_node];

                    // 如果西北方向的节点不是障碍物
                    if (!node.isObstacle)
                    {
                        // 检查它的南方和东方是否都不是障碍物
                        // 如果都不是障碍物，则该节点成为从南方和东方来时的跳点
                        if (isEmpty(getIndexOfNodeTowardsDirection(north_west_node, eDirections.SOUTH))
                            && isEmpty(getIndexOfNodeTowardsDirection(north_west_node, eDirections.EAST)))
                        {
                            node.isJumpPoint = true;
                            // 标记该节点是从南方来时的跳点
                            node.jumpPointDirection[(int)eDirections.SOUTH] = true;
                            // 标记该节点是从东方来时的跳点
                            node.jumpPointDirection[(int)eDirections.EAST] = true;
                        }
                    }
                }
            }
        }
    }

    // 构建直线跳点
    public void buildStraightJumpPoints()
    {
        // 计算东西方向上的跳点距离
        // 为网格中的每一行进行计算
        for (int row = 0; row < maxRows; ++row)
        {
            // 从左向右计算
            int jumpDistanceSoFar = -1; // 初始距离为-1，表示还未遇到跳点
            bool jumpPointSeen = false; // 标记是否已经看到跳点

            // 检查每个节点到其西侧（左侧）跳点的距离
            for (int column = 0; column < rowSize; ++column)
            {
                Node node = gridNodes[rowColumnToIndex(row, column)];

                // 如果是障碍物，重置计数并继续
                if (node.isObstacle)
                {
                    jumpDistanceSoFar = -1; // 重置距离
                    jumpPointSeen = false;  // 重置跳点标记
                    node.jpDistances[(int)eDirections.WEST] = 0; // 障碍物到跳点的距离为0
                    continue;
                }

                // 增加距离计数
                ++jumpDistanceSoFar;

                if (jumpPointSeen)
                {
                    // 如果已经看到了跳点，记录到最近一个跳点的距离
                    node.jpDistances[(int)eDirections.WEST] = jumpDistanceSoFar;
                }
                else
                {
                    // 如果还没有看到跳点，记录负值表示到墙壁的距离
                    node.jpDistances[(int)eDirections.WEST] = -jumpDistanceSoFar;
                }

                // 如果当前节点是从东方来时的跳点，则重置计数
                if (node.isJumpPointComingFrom(eDirections.EAST))
                {
                    jumpDistanceSoFar = 0; // 重置距离计数
                    jumpPointSeen = true;  // 标记已看到跳点
                }
            }

            // 从右向左计算
            jumpDistanceSoFar = -1; // 初始距离为-1，表示还未遇到跳点
            jumpPointSeen = false;  // 标记是否已经看到跳点
            
            // 检查每个节点到其东侧（右侧）跳点的距离
            for (int column = rowSize - 1; column >= 0; --column)
            {
                Node node = gridNodes[rowColumnToIndex(row, column)];

                // 如果是障碍物，重置计数并继续
                if (node.isObstacle)
                {
                    jumpDistanceSoFar = -1; // 重置距离
                    jumpPointSeen = false;  // 重置跳点标记
                    node.jpDistances[(int)eDirections.EAST] = 0; // 障碍物到跳点的距离为0
                    continue;
                }

                // 增加距离计数
                ++jumpDistanceSoFar;

                if (jumpPointSeen)
                {
                    // 如果已经看到了跳点，记录到最近一个跳点的距离
                    node.jpDistances[(int)eDirections.EAST] = jumpDistanceSoFar;
                }
                else
                {
                    // 如果还没有看到跳点，记录负值表示到墙壁的距离
                    node.jpDistances[(int)eDirections.EAST] = -jumpDistanceSoFar;
                }

                // 如果当前节点是从西方来时的跳点，则重置计数
                if (node.isJumpPointComingFrom(eDirections.WEST))
                {
                    jumpDistanceSoFar = 0; // 重置距离计数
                    jumpPointSeen = true;  // 标记已看到跳点
                }
            }
        }

        // 计算南北方向上的跳点距离
        // 为网格中的每一列进行计算
        for (int column = 0; column < rowSize; ++column)
        {
            // 从上向下计算
            int jumpDistanceSoFar = -1; // 初始距离为-1，表示还未遇到跳点
            bool jumpPointSeen = false; // 标记是否已经看到跳点

            // 检查每个节点到其北侧（上侧）跳点的距离
            for (int row = 0; row < maxRows; ++row)
            {
                Node node = gridNodes[rowColumnToIndex(row, column)];

                // 如果是障碍物，重置计数并继续
                if (node.isObstacle)
                {
                    jumpDistanceSoFar = -1; // 重置距离
                    jumpPointSeen = false;  // 重置跳点标记
                    node.jpDistances[(int)eDirections.NORTH] = 0; // 障碍物到跳点的距离为0
                    continue;
                }

                // 增加距离计数
                ++jumpDistanceSoFar;

                if (jumpPointSeen)
                {
                    // 如果已经看到了跳点，记录到最近一个跳点的距离
                    node.jpDistances[(int)eDirections.NORTH] = jumpDistanceSoFar;
                }
                else
                {
                    // 如果还没有看到跳点，记录负值表示到墙壁的距离
                    node.jpDistances[(int)eDirections.NORTH] = -jumpDistanceSoFar;
                }

                // 如果当前节点是从南方来时的跳点，则重置计数
                if (node.isJumpPointComingFrom(eDirections.SOUTH))
                {
                    jumpDistanceSoFar = 0; // 重置距离计数
                    jumpPointSeen = true;  // 标记已看到跳点
                }
            }

            // 从下向上计算
            jumpDistanceSoFar = -1; // 初始距离为-1，表示还未遇到跳点
            jumpPointSeen = false;  // 标记是否已经看到跳点
            
            // 检查每个节点到其南侧（下侧）跳点的距离
            for (int row = maxRows - 1; row >= 0; --row)
            {
                Node node = gridNodes[rowColumnToIndex(row, column)];

                // 如果是障碍物，重置计数并继续
                if (node.isObstacle)
                {
                    jumpDistanceSoFar = -1; // 重置距离
                    jumpPointSeen = false;  // 重置跳点标记
                    node.jpDistances[(int)eDirections.SOUTH] = 0; // 障碍物到跳点的距离为0
                    continue;
                }

                // 增加距离计数
                ++jumpDistanceSoFar;

                if (jumpPointSeen)
                {
                    // 如果已经看到了跳点，记录到最近一个跳点的距离
                    node.jpDistances[(int)eDirections.SOUTH] = jumpDistanceSoFar;
                }
                else
                {
                    // 如果还没有看到跳点，记录负值表示到墙壁的距离
                    node.jpDistances[(int)eDirections.SOUTH] = -jumpDistanceSoFar;
                }

                // 如果当前节点是从北方来时的跳点，则重置计数
                if (node.isJumpPointComingFrom(eDirections.NORTH))
                {
                    jumpDistanceSoFar = 0; // 重置距离计数
                    jumpPointSeen = true;  // 标记已看到跳点
                }
            }
        }
    }

    // 获取指定行列的节点
    private Node getNode(int row, int column)
    {
        Node node = null;

        // 检查坐标是否在有效范围内
        if (isInBounds(row, column))
        {
            // 获取对应索引的节点
            node = gridNodes[rowColumnToIndex(row, column)];
        }

        // 返回节点，可能为null
        return node;
    }

    // 构建对角线跳点
    public void buildDiagonalJumpPoints()
    {
        // Calcin' Jump Distance, Diagonally Upleft and upright
        // For all the rows in the grid
        for (int row = 0; row < maxRows; ++row)
        {
            // foreach column
            for (int column = 0; column < rowSize; ++column)
            {
                // if this node is an obstacle, then skip
                if (isObstacleOrWall(row, column)) continue;
                Node node = gridNodes[rowColumnToIndex(row, column)]; // Grab the node ( will not be NULL! )

                // Calculate NORTH WEST DISTNACES
                if (row == 0 || column == 0 || ( // If we in the north west corner
                        isObstacleOrWall(row - 1, column) || // If the node to the north is an obstacle
                        isObstacleOrWall(row, column - 1) || // If the node to the left is an obstacle
                        isObstacleOrWall(row - 1, column - 1))) // if the node to the North west is an obstacle
                {
                    // Wall one away
                    node.jpDistances[(int)eDirections.NORTH_WEST] = 0;
                }
                else if (isEmpty(row - 1, column) && // if the node to the north is empty
                         isEmpty(row, column - 1) && // if the node to the west is empty
                         (getNode(row - 1, column - 1).jpDistances[(int)eDirections.NORTH] >
                          0 || // If the node to the north west has is a straight jump point ( or primary jump point) going north
                          getNode(row - 1, column - 1).jpDistances[(int)eDirections.WEST] >
                          0)) // If the node to the north west has is a straight jump point ( or primary jump point) going West
                {
                    // Diagonal one away
                    node.jpDistances[(int)eDirections.NORTH_WEST] = 1;
                }
                else
                {
                    // Increment from last
                    int jumpDistance = getNode(row - 1, column - 1).jpDistances[(int)eDirections.NORTH_WEST];

                    if (jumpDistance > 0)
                    {
                        node.jpDistances[(int)eDirections.NORTH_WEST] = 1 + jumpDistance;
                    }
                    else //if( jumpDistance <= 0 )
                    {
                        node.jpDistances[(int)eDirections.NORTH_WEST] = -1 + jumpDistance;
                    }
                }

                // Calculate NORTH EAST DISTNACES
                if (row == 0 || column == rowSize - 1 || ( // If we in the top right corner
                        isObstacleOrWall(row - 1, column) || // If the node to the north is an obstacle
                        isObstacleOrWall(row, column + 1) || // If the node to the east is an obstacle
                        isObstacleOrWall(row - 1, column + 1))) // if the node to the North East is an obstacle
                {
                    // Wall one away
                    node.jpDistances[(int)eDirections.NORTH_EAST] = 0;
                }
                else if (isEmpty(row - 1, column) && // if the node to the north is empty
                         isEmpty(row, column + 1) && // if the node to the east is empty
                         (getNode(row - 1, column + 1).jpDistances[(int)eDirections.NORTH] >
                          0 || // If the node to the north east has is a straight jump point ( or primary jump point) going north
                          getNode(row - 1, column + 1).jpDistances[(int)eDirections.EAST] >
                          0)) // If the node to the north east has is a straight jump point ( or primary jump point) going east
                {
                    // Diagonal one away
                    node.jpDistances[(int)eDirections.NORTH_EAST] = 1;
                }
                else
                {
                    // Increment from last
                    int jumpDistance = getNode(row - 1, column + 1).jpDistances[(int)eDirections.NORTH_EAST];

                    if (jumpDistance > 0)
                    {
                        node.jpDistances[(int)eDirections.NORTH_EAST] = 1 + jumpDistance;
                    }
                    else //if( jumpDistance <= 0 )
                    {
                        node.jpDistances[(int)eDirections.NORTH_EAST] = -1 + jumpDistance;
                    }
                }
            }
        }

        // Calcin' Jump Distance, Diagonally DownLeft and Downright
        // For all the rows in the grid
        for (int row = maxRows - 1; row >= 0; --row)
        {
            // foreach column
            for (int column = 0; column < rowSize; ++column)
            {
                // if this node is an obstacle, then skip
                if (isObstacleOrWall(row, column)) continue;
                Node node = gridNodes[rowColumnToIndex(row, column)]; // Grab the node ( will not be NULL! )

                // Calculate SOUTH WEST DISTNACES
                if (row == maxRows - 1 || column == 0 || ( // If we in the south west most node
                        isObstacleOrWall(row + 1, column) || // If the node to the south is an obstacle
                        isObstacleOrWall(row, column - 1) || // If the node to the west is an obstacle
                        isObstacleOrWall(row + 1, column - 1))) // if the node to the south West is an obstacle
                {
                    // Wall one away
                    node.jpDistances[(int)eDirections.SOUTH_WEST] = 0;
                }
                else if (isEmpty(row + 1, column) && // if the node to the south is empty
                         isEmpty(row, column - 1) && // if the node to the west is empty
                         (getNode(row + 1, column - 1).jpDistances[(int)eDirections.SOUTH] >
                          0 || // If the node to the south west has is a straight jump point ( or primary jump point) going south
                          getNode(row + 1, column - 1).jpDistances[(int)eDirections.WEST] >
                          0)) // If the node to the south west has is a straight jump point ( or primary jump point) going West
                {
                    // Diagonal one away
                    node.jpDistances[(int)eDirections.SOUTH_WEST] = 1;
                }
                else
                {
                    // Increment from last
                    int jumpDistance = getNode(row + 1, column - 1).jpDistances[(int)eDirections.SOUTH_WEST];

                    if (jumpDistance > 0)
                    {
                        node.jpDistances[(int)eDirections.SOUTH_WEST] = 1 + jumpDistance;
                    }
                    else //if( jumpDistance <= 0 )
                    {
                        node.jpDistances[(int)eDirections.SOUTH_WEST] = -1 + jumpDistance;
                    }
                }

                // Calculate SOUTH EAST DISTNACES
                if (row == maxRows - 1 || column == rowSize - 1 || ( // If we in the south east corner
                        isObstacleOrWall(row + 1, column) || // If the node to the south is an obstacle
                        isObstacleOrWall(row, column + 1) || // If the node to the east is an obstacle
                        isObstacleOrWall(row + 1, column + 1))) // if the node to the south east is an obstacle
                {
                    // Wall one away
                    node.jpDistances[(int)eDirections.SOUTH_EAST] = 0;
                }
                else if (isEmpty(row + 1, column) && // if the node to the south is empty
                         isEmpty(row, column + 1) && // if the node to the east is empty
                         (getNode(row + 1, column + 1).jpDistances[(int)eDirections.SOUTH] >
                          0 || // If the node to the south east has is a straight jump point ( or primary jump point) going south
                          getNode(row + 1, column + 1).jpDistances[(int)eDirections.EAST] >
                          0)) // If the node to the south east has is a straight jump point ( or primary jump point) going east
                {
                    // Diagonal one away
                    node.jpDistances[(int)eDirections.SOUTH_EAST] = 1;
                }
                else
                {
                    // Increment from last
                    int jumpDistance = getNode(row + 1, column + 1).jpDistances[(int)eDirections.SOUTH_EAST];

                    if (jumpDistance > 0)
                    {
                        node.jpDistances[(int)eDirections.SOUTH_EAST] = 1 + jumpDistance;
                    }
                    else //if( jumpDistance <= 0 )
                    {
                        node.jpDistances[(int)eDirections.SOUTH_EAST] = -1 + jumpDistance;
                    }
                }
            }
        }
    }

    // 八方向启发式函数常量
    static readonly float SQRT_2 = Mathf.Sqrt(2); // 根号2，对角线移动的代价
    static readonly float SQRT_2_MINUS_1 = Mathf.Sqrt(2) - 1.0f; // 根号2减1，用于八方向启发式计算

    // 计算八方向启发式值
    internal static int octileHeuristic(int curr_row, int curr_column, int goal_row, int goal_column)
    {
        int heuristic;
        
        // 计算行和列的距离差
        int row_dist = Mathf.Abs(goal_row - curr_row);
        int column_dist = Mathf.Abs(goal_column - curr_column);

        // 八方向启发式: 最大值(行距离,列距离) + (根号2-1) * 最小值(行距离,列距离)
        // 这样计算可以准确估计通过正交和对角线移动到达目标的最短距离
        heuristic = (int)(Mathf.Max(row_dist, column_dist) + SQRT_2_MINUS_1 * Mathf.Min(row_dist, column_dist));

        return heuristic;
    }

    // 获取所有有效方向
    private eDirections[] getAllValidDirections(PathfindingNode curr_node)
    {
        // 如果当前节点没有父节点，则可以向所有方向移动
        // 否则，根据从父节点到当前节点的方向，确定可以继续移动的方向
        return curr_node.parent == null ? allDirections : validDirLookUpTable[curr_node.directionFromParent];
    }

    // 判断是否为基本方向（北、东、南、西）
    private bool isCardinal(eDirections dir)
    {
        switch (dir)
        {
            case eDirections.SOUTH:
            case eDirections.EAST:
            case eDirections.NORTH:
            case eDirections.WEST:
                return true;
        }

        return false;
    }

    // 判断是否为对角线方向
    private bool isDiagonal(eDirections dir)
    {
        switch (dir)
        {
            case eDirections.SOUTH_EAST:
            case eDirections.SOUTH_WEST:
            case eDirections.NORTH_EAST:
            case eDirections.NORTH_WEST:
                return true;
        }

        return false;
    }

    // 判断目标是否在精确方向上
    private bool goalIsInExactDirection(Point curr, eDirections dir, Point goal)
    {
        // 计算目标点与当前点的列和行差值
        int diff_column = goal.column - curr.column;
        int diff_row = goal.row - curr.row;

        // 注意：北方向对应行减少，而不是增加。行坐标向南增长！
        switch (dir)
        {
            case eDirections.NORTH:
                // 目标在正北方：行差为负，列差为0
                return diff_row < 0 && diff_column == 0;
            case eDirections.NORTH_EAST:
                // 目标在东北方：行差为负，列差为正，且行列差的绝对值相等
                return diff_row < 0 && diff_column > 0 && Mathf.Abs(diff_row) == Mathf.Abs(diff_column);
            case eDirections.EAST:
                // 目标在正东方：行差为0，列差为正
                return diff_row == 0 && diff_column > 0;
            case eDirections.SOUTH_EAST:
                // 目标在东南方：行差为正，列差为正，且行列差的绝对值相等
                return diff_row > 0 && diff_column > 0 && Mathf.Abs(diff_row) == Mathf.Abs(diff_column);
            case eDirections.SOUTH:
                // 目标在正南方：行差为正，列差为0
                return diff_row > 0 && diff_column == 0;
            case eDirections.SOUTH_WEST:
                // 目标在西南方：行差为正，列差为负，且行列差的绝对值相等
                return diff_row > 0 && diff_column < 0 && Mathf.Abs(diff_row) == Mathf.Abs(diff_column);
            case eDirections.WEST:
                // 目标在正西方：行差为0，列差为负
                return diff_row == 0 && diff_column < 0;
            case eDirections.NORTH_WEST:
                // 目标在西北方：行差为负，列差为负，且行列差的绝对值相等
                return diff_row < 0 && diff_column < 0 && Mathf.Abs(diff_row) == Mathf.Abs(diff_column);
        }

        return false;
    }

    // 判断目标是否在一般方向上
    private bool goalIsInGeneralDirection(Point curr, eDirections dir, Point goal)
    {
        // 计算目标点与当前点的列和行差值
        int diff_column = goal.column - curr.column;
        int diff_row = goal.row - curr.row;

        // 注意：北方向对应行减少，而不是增加。行坐标向南增长！
        switch (dir)
        {
            case eDirections.NORTH:
                // 目标在北方：行差为负，列差为0
                return diff_row < 0 && diff_column == 0;
            case eDirections.NORTH_EAST:
                // 目标在东北方向区域：行差为负，列差为正
                return diff_row < 0 && diff_column > 0;
            case eDirections.EAST:
                // 目标在东方：行差为0，列差为正
                return diff_row == 0 && diff_column > 0;
            case eDirections.SOUTH_EAST:
                // 目标在东南方向区域：行差为正，列差为正
                return diff_row > 0 && diff_column > 0;
            case eDirections.SOUTH:
                // 目标在南方：行差为正，列差为0
                return diff_row > 0 && diff_column == 0;
            case eDirections.SOUTH_WEST:
                // 目标在西南方向区域：行差为正，列差为负
                return diff_row > 0 && diff_column < 0;
            case eDirections.WEST:
                // 目标在西方：行差为0，列差为负
                return diff_row == 0 && diff_column < 0;
            case eDirections.NORTH_WEST:
                // 目标在西北方向区域：行差为负，列差为负
                return diff_row < 0 && diff_column < 0;
        }

        return false;
    }

    // 获取指定距离的节点
    private PathfindingNode getNodeDist(int row, int column, eDirections direction, int dist)
    {
        // 初始化结果变量
        PathfindingNode new_node = null;
        int new_row = row, new_column = column;

        // 根据方向和距离计算目标位置的行列坐标
        switch (direction)
        {
            case eDirections.NORTH:
                // 北方向：行减少
                new_row -= dist;
                break;
            case eDirections.NORTH_EAST:
                // 东北方向：行减少，列增加
                new_row -= dist;
                new_column += dist;
                break;
            case eDirections.EAST:
                // 东方向：列增加
                new_column += dist;
                break;
            case eDirections.SOUTH_EAST:
                // 东南方向：行增加，列增加
                new_row += dist;
                new_column += dist;
                break;
            case eDirections.SOUTH:
                // 南方向：行增加
                new_row += dist;
                break;
            case eDirections.SOUTH_WEST:
                // 西南方向：行增加，列减少
                new_row += dist;
                new_column -= dist;
                break;
            case eDirections.WEST:
                // 西方向：列减少
                new_column -= dist;
                break;
            case eDirections.NORTH_WEST:
                // 西北方向：行减少，列减少
                new_row -= dist;
                new_column -= dist;
                break;
        }

        // 检查新的坐标是否在地图范围内
        if (isInBounds(new_row, new_column))
        {
            // 获取对应的寻路节点
            new_node = this.pathfindingNodes[this.rowColumnToIndex(new_row, new_column)];
        }

        // 返回找到的节点，可能为null
        return new_node;
    }

    // 重建路径
    public List<Point> reconstructPath(PathfindingNode goal, Point start)
    {
        // 创建存储路径的列表
        List<Point> path = new List<Point>();
        // 从目标节点开始
        PathfindingNode curr_node = goal;

        // 从目标节点回溯到起点前的节点
        while (curr_node.parent != null)
        {
            // 将当前节点的位置添加到路径中
            path.Add(curr_node.pos);
            // 移动到父节点
            curr_node = curr_node.parent;
        }

        // 添加起点到路径中
        path.Add(start);

        // 将路径反转，使其从起点到终点
        path.Reverse();
        
        // 返回完整路径
        return path;
    }

    // 异步获取路径
    public IEnumerator getPathAsync(Point start, Point goal)
    {
        // 创建开放列表（优先队列），按路径代价排序
        PriorityQueue<PathfindingNode, float> open_set = new PriorityQueue<PathfindingNode, float>();
        bool found_path = false;
        // 创建路径查找返回对象
        PathfindReturn return_status = new PathfindReturn();

        // 重置所有寻路节点的数据
        ResetPathfindingNodeData();
        // 获取起点节点并初始化
        PathfindingNode starting_node = this.pathfindingNodes[pointToIndex(start)];
        starting_node.pos = start;
        starting_node.parent = null;
        starting_node.givenCost = 0;  // 起点到自身的代价为0
        starting_node.finalCost = 0;  // 总代价初始为0
        starting_node.listStatus = ListStatus.ON_OPEN;  // 标记为在开放列表中

        // 将起点节点加入开放列表，优先级为0
        open_set.push(starting_node, 0);

        // 当开放列表不为空时循环
        while (!open_set.isEmpty())
        {
            // 从开放列表中取出代价最小的节点
            PathfindingNode curr_node = open_set.pop();
            PathfindingNode parent = curr_node.parent;
            // 获取当前节点的跳点信息
            Node jp_node = gridNodes[pointToIndex(curr_node.pos)];

            // 更新当前处理的节点
            return_status._current = curr_node;

            // 检查是否已到达目标
            if (curr_node.pos.Equals(goal))
            {
                // 找到路径，重建并返回
                return_status.path = reconstructPath(curr_node, start);
                return_status._status = PathfindReturn.PathfindStatus.FOUND;
                found_path = true;
                yield return return_status;
                break;
            }

            // 每次迭代返回当前状态，允许可视化过程
            yield return return_status;

            // 遍历当前节点的所有有效方向
            foreach (eDirections dir in getAllValidDirections(curr_node))
            {
                PathfindingNode new_successor = null;
                int given_cost = 0;

                // 情况1：目标在当前方向的直线上，且比跳点或墙壁更近
                // 如果是基本方向，目标在精确方向上，且距离小于等于跳点距离
                if (isCardinal(dir) &&
                    goalIsInExactDirection(curr_node.pos, dir, goal) &&
                    Point.diff(curr_node.pos, goal) <= Mathf.Abs(jp_node.jpDistances[(int)dir]))
                {
                    // 直接将目标设为后继节点
                    new_successor = this.pathfindingNodes[pointToIndex(goal)];
                    // 计算从起点经过当前节点到目标的代价
                    given_cost = curr_node.givenCost + Point.diff(curr_node.pos, goal);
                }
                // 情况2：目标在当前方向的对角线区域内，且在行或列上比跳点或墙壁更近
                else if (isDiagonal(dir) &&
                         goalIsInGeneralDirection(curr_node.pos, dir, goal) &&
                         (Mathf.Abs(goal.column - curr_node.pos.column) <= Mathf.Abs(jp_node.jpDistances[(int)dir]) ||
                          Mathf.Abs(goal.row - curr_node.pos.row) <= Mathf.Abs(jp_node.jpDistances[(int)dir])))
                {
                    // 计算目标与当前节点的最小行列差
                    int min_diff = Mathf.Min(Mathf.Abs(goal.column - curr_node.pos.column),
                        Mathf.Abs(goal.row - curr_node.pos.row));

                    // 获取从当前节点沿指定方向移动min_diff距离的节点
                    new_successor = getNodeDist(
                        curr_node.pos.row,
                        curr_node.pos.column,
                        dir,
                        min_diff);

                    // 计算代价，考虑对角线移动的额外代价
                    given_cost = curr_node.givenCost + (int)(SQRT_2 * Point.diff(curr_node.pos, new_successor.pos));
                }
                // 情况3：当前方向上有跳点
                else if (jp_node.jpDistances[(int)dir] > 0)
                {
                    // 获取从当前节点沿指定方向到跳点的节点
                    new_successor = getNodeDist(
                        curr_node.pos.row,
                        curr_node.pos.column,
                        dir,
                        jp_node.jpDistances[(int)dir]);

                    // 计算到跳点的距离
                    given_cost = Point.diff(curr_node.pos, new_successor.pos);

                    // 如果是对角线方向，需要乘以根号2
                    if (isDiagonal(dir))
                    {
                        given_cost = (int)(given_cost * SQRT_2);
                    }

                    // 加上从起点到当前节点的代价
                    given_cost += curr_node.givenCost;
                }

                // 使用A*算法处理后继节点
                if (new_successor != null)
                {
                    // 如果后继节点不在开放列表中
                    if (new_successor.listStatus != ListStatus.ON_OPEN)
                    {
                        // 设置父节点、代价和方向
                        new_successor.parent = curr_node;
                        new_successor.givenCost = given_cost;
                        new_successor.directionFromParent = dir;
                        // 计算总代价 = 实际代价 + 启发式代价
                        new_successor.finalCost = given_cost + octileHeuristic(new_successor.pos.column,
                            new_successor.pos.row, goal.column, goal.row);
                        new_successor.listStatus = ListStatus.ON_OPEN;
                        // 加入开放列表
                        open_set.push(new_successor, new_successor.finalCost);
                    }
                    // 如果后继节点已在开放列表，且新路径更优
                    else if (given_cost < new_successor.givenCost)
                    {
                        // 更新节点信息
                        new_successor.parent = curr_node;
                        new_successor.givenCost = given_cost;
                        new_successor.directionFromParent = dir;
                        new_successor.finalCost = given_cost + octileHeuristic(new_successor.pos.column,
                            new_successor.pos.row, goal.column, goal.row);
                        new_successor.listStatus = ListStatus.ON_OPEN;
                        // 更新开放列表中的节点
                        open_set.push(new_successor, new_successor.finalCost);
                    }
                }
            }
        }

        // 如果没找到路径，更新状态为未找到
        if (!found_path)
        {
            return_status._status = PathfindReturn.PathfindStatus.NOT_FOUND;
            yield return return_status;
        }
    }

    // 同步获取路径
    public List<Point> getPath(Point start, Point goal)
    {
        // 创建路径列表
        List<Point> path = new List<Point>();
        // 创建开放列表（优先队列），按路径代价排序
        PriorityQueue<PathfindingNode, float> open_set = new PriorityQueue<PathfindingNode, float>();

        // 重置所有寻路节点的数据
        ResetPathfindingNodeData();
        // 获取起点节点并初始化
        PathfindingNode starting_node = this.pathfindingNodes[pointToIndex(start)];
        starting_node.pos = start;
        starting_node.parent = null;
        starting_node.givenCost = 0;  // 起点到自身的代价为0
        starting_node.finalCost = 0;  // 总代价初始为0
        starting_node.listStatus = ListStatus.ON_OPEN;  // 标记为在开放列表中

        // 将起点节点加入开放列表，优先级为0
        open_set.push(starting_node, 0);

        // 当开放列表不为空时循环
        while (!open_set.isEmpty())
        {
            // 从开放列表中取出代价最小的节点
            PathfindingNode curr_node = open_set.pop();
            PathfindingNode parent = curr_node.parent;
            // 获取当前节点的跳点信息
            Node jp_node = gridNodes[pointToIndex(curr_node.pos)];

            // 检查是否已到达目标
            if (curr_node.pos.Equals(goal))
            {
                // 找到路径，重建并返回
                return reconstructPath(curr_node, start);
            }

            // 遍历当前节点的所有有效方向
            foreach (eDirections dir in getAllValidDirections(curr_node))
            {
                PathfindingNode new_successor = null;
                int given_cost = 0;

                // 情况1：目标在当前方向的直线上，且比跳点或墙壁更近
                if (isCardinal(dir) &&
                    goalIsInExactDirection(curr_node.pos, dir, goal) &&
                    Point.diff(curr_node.pos, goal) <= Mathf.Abs(jp_node.jpDistances[(int)dir]))
                {
                    // 直接将目标设为后继节点
                    new_successor = this.pathfindingNodes[pointToIndex(goal)];
                    // 计算从起点经过当前节点到目标的代价
                    given_cost = curr_node.givenCost + Point.diff(curr_node.pos, goal);
                }
                // 情况2：目标在当前方向的对角线区域内，且在行或列上比跳点或墙壁更近
                else if (isDiagonal(dir) &&
                         goalIsInGeneralDirection(curr_node.pos, dir, goal) &&
                         (Mathf.Abs(goal.column - curr_node.pos.column) <= Mathf.Abs(jp_node.jpDistances[(int)dir]) ||
                          Mathf.Abs(goal.row - curr_node.pos.row) <= Mathf.Abs(jp_node.jpDistances[(int)dir])))
                {
                    // 计算目标与当前节点的最小行列差
                    int min_diff = Mathf.Min(Mathf.Abs(goal.column - curr_node.pos.column),
                        Mathf.Abs(goal.row - curr_node.pos.row));

                    // 获取从当前节点沿指定方向移动min_diff距离的节点
                    new_successor = getNodeDist(
                        curr_node.pos.row,
                        curr_node.pos.column,
                        dir,
                        min_diff);

                    // 计算代价，考虑对角线移动的额外代价
                    given_cost = curr_node.givenCost + (int)(SQRT_2 * Point.diff(curr_node.pos, new_successor.pos));
                }
                // 情况3：当前方向上有跳点
                else if (jp_node.jpDistances[(int)dir] > 0)
                {
                    // 获取从当前节点沿指定方向到跳点的节点
                    new_successor = getNodeDist(
                        curr_node.pos.row,
                        curr_node.pos.column,
                        dir,
                        jp_node.jpDistances[(int)dir]);

                    // 计算到跳点的距离
                    given_cost = Point.diff(curr_node.pos, new_successor.pos);

                    // 如果是对角线方向，需要乘以根号2
                    if (isDiagonal(dir))
                    {
                        given_cost = (int)(given_cost * SQRT_2);
                    }

                    // 加上从起点到当前节点的代价
                    given_cost += curr_node.givenCost;
                }

                // 使用A*算法处理后继节点
                if (new_successor != null)
                {
                    // 如果后继节点不在开放列表中
                    if (new_successor.listStatus != ListStatus.ON_OPEN)
                    {
                        // 设置父节点、代价和方向
                        new_successor.parent = curr_node;
                        new_successor.givenCost = given_cost;
                        new_successor.directionFromParent = dir;
                        // 计算总代价 = 实际代价 + 启发式代价
                        new_successor.finalCost = given_cost + octileHeuristic(new_successor.pos.column,
                            new_successor.pos.row, goal.column, goal.row);
                        new_successor.listStatus = ListStatus.ON_OPEN;
                        // 加入开放列表
                        open_set.push(new_successor, new_successor.finalCost);
                    }
                    // 如果后继节点已在开放列表，且新路径更优
                    else if (given_cost < new_successor.givenCost)
                    {
                        // 更新节点信息
                        new_successor.parent = curr_node;
                        new_successor.givenCost = given_cost;
                        new_successor.directionFromParent = dir;
                        // 计算总代价
                        new_successor.finalCost = given_cost + octileHeuristic(new_successor.pos.column,
                            new_successor.pos.row, goal.column, goal.row);
                        new_successor.listStatus = ListStatus.ON_OPEN;
                        // 更新开放列表中的节点
                        open_set.push(new_successor, new_successor.finalCost);
                    }
                }
            }
        }

        // 没找到路径，返回空列表
        return path;
    }

    // 重置路径查找节点数据
    public void ResetPathfindingNodeData()
    {
        // 遍历所有路径查找节点
        foreach (var node in this.pathfindingNodes)
        {
            // 调用每个节点的Reset方法重置数据
            node.Reset();
        }
    }
}