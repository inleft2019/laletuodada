using System.Collections;
using System.Collections.Generic;
using System.Threading;
using script.model;
using script.utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace script.test
{
    /// <summary>
    /// Creator@BiliBili 笔墨v
    /// since 202406
    /// </summary>
    public class Main : MonoBehaviour
    {
        public static int meshSize = 20; //网格大小


        private static Vector3[] array2 = new Vector3[meshSize + 1];
        private static Vector3[] array3 = new Vector3[meshSize + 1];

        private static HashSet<Vector3> obstacleSet = new(); //地图障碍
        private static bool[,] _mapInfo = new bool[meshSize, meshSize]; //另一种表示地图障碍的方法


        private static Vector3 startPoint;
        private static Vector3 endPoint;

        private static Vector3 checkingPoint; //ui 调试,上次扫描结算点
        private static Vector3 checkingPointParent; //ui调试,正在扫描点的父节点
        private static Vector3 wantToNextPoint; //正在扫描点

        //debug数据点位
        private static List<MapPoint> showData = new();

        //打开列表,用于查重
        private static HashSet<Vector3> openSet = new();

        //打开列表,启发函数值排序
        // private static SortedSet<MapPoint> openSortedSet = new(new MapPointComparer(endPoint));
        private static List<MapPoint> openList = new();

        //关闭列表
        private static HashSet<Vector3> closeSet = new();


        private static List<Vector3> path = new(); //寻路路线

        static Main()
        {
            for (int i = 0; i <= meshSize; i++)
            {
                array2[i].z = i * 1F;
            }

            for (int i = 0; i <= meshSize; i++)
            {
                array3[i].x = i * 1F;
            }

            #region 绘制网格

            //绘制网格
            //Vector3 [][] array=new Vector3[10][];
            /*for (int i = 0; i < 10; i++)
            {
                array[i] = new Vector3[10];
                for (int j = 0; j < 10; j++)
                {
                    array[i][j].x += i * 10;
                    array[i][j].z += j * 10;
                }
            }
            for (int i = 0; i < 10; i++)
            {
                Gizmos.DrawLineStrip(array[i], false);
            }
            */

            #endregion
        }

        #region ui组件

        public Toggle obstacle;
        public Toggle removeObstacle;
        public Toggle start;
        public Toggle end;
        public Toggle dataModel;
        public Button calculate;
        public Button reset;
        public Button clearGraph;
        public Button stop;
        public Button step;
        public Slider slider;
        public Text data;

        public Toggle aStar;
        public Toggle JPS;
        public Toggle thetaStar;

        public Toggle optimalPathStatusCalculate;

        public bool optimalPathStatus = false;

        public bool stopStatus;
        public int selectIndex;
        public int selectFindType;
        public bool stepStatus;

        #endregion

        private WaitForSeconds _waitForSeconds = new WaitForSeconds(0.3F);
        private WaitForSeconds _stopSecond = new WaitForSeconds(0.1F);
        private WaitForSeconds _stepSecond = new WaitForSeconds(0.3F);


        [MenuItem("inleft工具/Test")]
        public static void Test()
        {
            //加入点测试
            SortedSet<MapPoint> sortedSet = new(new MapPointComparer(Main.endPoint));

            Vector3 endPoint = new Vector3(0, 0, 6);
            Vector3 v1 = new Vector3(-1, 0, 2);
            Vector3 v2 = new Vector3(1, 0, 2);
            Vector3 v3 = new Vector3(1, 0, 3);

            MapPoint startPoint = new(null, Vector3.zero, endPoint);
            MapPoint point1 = new(startPoint, v1, endPoint);
            MapPoint point2 = new(startPoint, v2, endPoint);
            MapPoint point3 = new(startPoint, v3, endPoint);


            sortedSet.Add(point1);
            sortedSet.Add(point3);
            print(sortedSet.Contains(point2));
            print(sortedSet.Max.position);
            print(sortedSet.Min.position);
            print(sortedSet.Remove(point3));
            print(sortedSet.Contains(point3));


            /*Vector3 v1 = new Vector3(-1, 0, 2);
            Vector3 v2 = new Vector3(1, 0, 2);
            Vector3 v3 = new Vector3(1, 0, 3);

            HashSet<Vector3> set = new HashSet<Vector3>();

            set.Add(v1);
            print(set.Contains(v2));*/
        }

        //超过三个点才需要进来判断  
        //每个格子和祖父节点相连判断
        //两格子之间之间没有障碍,则继续判断祖父节点的父节点,一直到遇到障碍或者到尽头,再和最后一次格子直接相连
        //新连的格子不是尽头,回开头,继续这个过程
        public void RayTestObstacle(MapPoint mapPoint, MapPoint nextPoint)
        {
            path.Add(mapPoint.position);
            while (true)
            {
                nextPoint = this.Recusive(mapPoint, nextPoint);
                path.Add(nextPoint.position);

                //最后一个节点能连上
                if (nextPoint.parent == null)
                    break;

                mapPoint = nextPoint;
                nextPoint = nextPoint.parent;
            }
        }

        //获取点与点之间无障碍的最短连线
        private MapPoint Recusive(MapPoint start, MapPoint end)
        {
            //尽头
            if (end.parent == null)
                return end;

            // if (!this.hasObstacle(start.position, end.parent.position))
            if (!MyGridHelper.GetTouchedPosBetweenTwoPoints(start.position, end.parent.position, _mapInfo))
            {
                //没有阻挡,下一位..
                return Recusive(start, end.parent);
            }

            return end;
        }

        //两点是否有障碍
        public bool hasObstacle(Vector3 v1, Vector3 v2)
        {
            /*foreach (Vector2Int item in GridHelper.GetTouchedPosBetweenTwoPoints(
                         new Vector2Int((int)v1.x, (int)v1.z),
                         new Vector2Int((int)v2.x, (int)v2.z)))
            {
                if (_mapInfo[item.x, item.y])
                    return true;
            }*/

            return false;
        }

        //绘制网格
        private static void GraphMesh()
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i <= meshSize; i++)
                Gizmos.DrawRay(array2[i], Vector3.right * meshSize);
            for (int i = 0; i <= meshSize; i++)
                Gizmos.DrawRay(array3[i], Vector3.forward * meshSize); //z=>forword
        }


        //jps寻路
        IEnumerator JPSFindPath()
        {
            //数据清理
            openSet.Clear();
            openList.Clear();
            closeSet.Clear();
            path.Clear();
            showData.Clear();
            checkingPoint = Vector3.zero;
            checkingPointParent = Vector3.zero;
            wantToNextPoint = Vector3.zero;


            MapPoint start = new(null, startPoint, endPoint);
            openSet.Add(startPoint);
            openList.Add(start);

            showData.Add(start);

            MapPointComparer mapPointComparer = new(Main.endPoint);
            MapPoint result = null;

            while (true)
            {
                if (openList.Count == 0)
                    break; //死路

                //1.从开启列表中找最佳点位
                openList.Sort(mapPointComparer);
                MapPoint checkMapPoint = openList[0]; //排序
                Vector3 checkPoint = checkMapPoint.position;

                Vector3 parentPoint = Vector3.one * -1;
                if (checkMapPoint.parent != null)
                    parentPoint = checkMapPoint.parent.position;

                //该点位退出开启列,进入关闭列表
                openList.Remove(checkMapPoint);
                openSet.Remove(checkPoint);
                closeSet.Add(checkPoint);

                if (checkPoint == endPoint)
                {
                    result = checkMapPoint;
                    break; //找到终点
                }

                checkingPoint = checkPoint;
                if (checkMapPoint.parent != null)
                    checkingPointParent = checkMapPoint.parent.position;


                //遍历四个方向,这里是斜向 ↙↗ ↖↘
                for (int x = -1; x <= 1; x++)
                {
                    if (x == 0) continue;

                    for (int z = -1; z <= 1; z++)
                    {
                        if (z == 0) continue;

                        //开始递归
                        this.RecursiveCheck(checkPoint, x, z, checkMapPoint, parentPoint, false);
                    }
                }

                //ui 调试
                yield return _waitForSeconds;

                while (this.stopStatus)
                {
                    yield return this._stopSecond;
                }

                if (this.stepStatus)
                {
                    this.stopStatus = !this.stopStatus;
                }
            }

            if (result != null)
            {
                if (this.selectFindType == 2)
                {
                    MapPoint tempPoint = result;
                    //确定路线,从终点往回遍历链表
                    path.Add(result.position);
                    while (true)
                    {
                        tempPoint = tempPoint.parent;
                        if (tempPoint == null) break;
                        path.Add(tempPoint.position);
                    }
                }

                else
                {
                    RayTestObstacle(result, result.parent);
                }

                print("寻路完成..");
            }
            else
            {
                print("死路...");
            }
        }

        /// <summary>
        /// 作为斜点的递归查询
        /// </summary>
        /// <param name="checkPoint">本轮检测节点</param>
        /// <param name="x">x轴变化</param>
        /// <param name="z">z轴变化</param>
        /// <param name="checkMapPoint">原始扩散节点(所有检测节点最初的起点)</param>
        /// <param name="fromPoint">本轮检测节点的父节点</param>
        /// <param name="isRecursivePoint">是否为递归状态</param>
        private void RecursiveCheck(Vector3 checkPoint, int x, int z, MapPoint checkMapPoint, Vector3 fromPoint, bool isRecursivePoint)
        {
            //根据强迫邻居的方向进行斜线扩展(并且进行对角线障碍通行判断),无父节点的原始节点(八向扩展)
            int xDirection = 0;
            int zDirection = 0;

            this.GetForcePointDirection(checkPoint, x, z, checkMapPoint, fromPoint, isRecursivePoint, ref xDirection, ref zDirection);

            if (x != xDirection || z != zDirection)
                return;

            if (this.FindObliquePoint(checkPoint, x, z, checkMapPoint, fromPoint, isRecursivePoint)) return;

            //该斜点是否为终点
            if (endPoint.x == checkPoint.x && endPoint.z == checkPoint.z)
            {
                this.AddPoint(checkMapPoint, endPoint);
                return;
            }

            //如果是空父结点,横向扫描时,需要立刻把强迫邻居加入打开列表
            //有父节点的,检测到强迫邻居,应该退出,任意横竖向有强迫邻居,本斜点加入打开列表
            //斜点进行本轮的横向检测
            bool xResult = false;
            bool zResult = false;


            if (checkMapPoint.parent != null || isRecursivePoint || xDirection * zDirection == 1)
            {
                for (int xAddIndex = (int)checkPoint.x + xDirection; xDirection > 0 ? (xAddIndex < meshSize) : xAddIndex >= 0;)
                {
                    //横向途中发现强迫邻居(障碍物),本斜点(排除空父节点)将加入打开列表,中断横向检测
                    if (this.HorizontalStep(xAddIndex, checkPoint, checkMapPoint, xDirection, isRecursivePoint, ref xResult))
                        break;

                    xAddIndex += xDirection;
                }
            }

            //递归点横竖向途中发现强迫邻居(障碍物),本递归点将加入打开列表
            if (isRecursivePoint && (xResult || zResult))
            {
                Vector3 current = new(checkPoint.x, 0, checkPoint.z);
                this.AddPoint(checkMapPoint, current);
                return;
            }

            //如果是空父节点,横向检测一条就够了,轮转一周刚好覆盖满
            if (checkMapPoint.parent != null || isRecursivePoint || xDirection * zDirection == -1)
            {
                //竖向途中发现强迫邻居(障碍物),本斜点(排除空父节点)将加入打开列表,中断竖向检测
                for (int zAddIndex = (int)checkPoint.z + zDirection; zDirection > 0 ? (zAddIndex < meshSize) : zAddIndex >= 0;)
                {
                    if (this.VerticalStep(zAddIndex, checkPoint, checkMapPoint, zDirection, isRecursivePoint, ref zResult))
                        break;
                    zAddIndex += zDirection;
                }
            }

            //递归点横竖向途中发现强迫邻居(障碍物),本递归点将加入打开列表
            if (isRecursivePoint && (xResult || zResult))
            {
                Vector3 current = new(checkPoint.x, 0, checkPoint.z);
                this.AddPoint(checkMapPoint, current);
                return;
            }

            //全部横竖斜向全部扫描完,开始本斜线上的下一个点,斜点斜向前进

            //拓展点,边界判断
            if (this.IsOutBoundary(checkPoint.x + x, checkPoint.z + z))
                return;

            //在障碍列表(地图数据生成)
            if (_mapInfo[(int)checkPoint.x + x, (int)checkPoint.z + z])
                return;

            //通行障碍判断
            if ((this.IsOutBoundary(checkPoint.x, checkPoint.z + z) || _mapInfo[(int)checkPoint.x, (int)checkPoint.z + z])
                && (this.IsOutBoundary(checkPoint.x + x, checkPoint.z) || _mapInfo[(int)checkPoint.x + x, (int)checkPoint.z]))
                return;

            Vector3 next = new(checkPoint.x + x, 0, checkPoint.z + z);
            wantToNextPoint = next;

            if (closeSet.Contains(next))
                return; //在关闭列表中,跳过

            //如果这里不允许重复的打开列表点,就是在尝试更多的最佳路径
            if (!this.optimalPathStatus && openSet.Contains(next))
                return; //已经在开启列表中,跳过

            //递归入口,当前斜点位移+1,继续往前执行,(直到遇到边界/障碍)
            this.RecursiveCheck(next, x, z, checkMapPoint, checkPoint, true);
        }

        //找斜点的强迫邻居
        private bool FindObliquePoint(Vector3 checkPoint, int x, int z, MapPoint checkMapPoint, Vector3 fromPoint, bool isRecursivePoint)
        {
            //父节点发现强迫节点,且强迫节点不是障碍物,本斜点加入打开列表,退出递归
            //到达边界/或者被阻挡前进(除非是终点),放弃
            if (isRecursivePoint
                && ((!this.IsOutBoundary(fromPoint.x + x, fromPoint.z)
                     && _mapInfo[(int)fromPoint.x + x, (int)fromPoint.z]
                     && !this.IsOutBoundary(fromPoint.x + x * 2, fromPoint.z)
                     && !_mapInfo[(int)fromPoint.x + x * 2, (int)fromPoint.z]
                    )
                    && (
                        (endPoint.x == fromPoint.x + x * 2 && endPoint.z == fromPoint.z)
                        ||
                        (
                            (!this.IsOutBoundary(checkPoint.x, checkPoint.z + z) && !_mapInfo[(int)checkPoint.x, (int)checkPoint.z + z])
                            || (!this.IsOutBoundary(checkPoint.x + x, checkPoint.z) && !_mapInfo[(int)checkPoint.x + x, (int)checkPoint.z])
                        )
                    )
                    ||
                    (!this.IsOutBoundary(fromPoint.x, fromPoint.z + z)
                     && _mapInfo[(int)fromPoint.x, (int)fromPoint.z + z]
                     && !this.IsOutBoundary(fromPoint.x, fromPoint.z + z * 2)
                     && !_mapInfo[(int)fromPoint.x, (int)fromPoint.z + z * 2]
                    )
                    && (
                        (endPoint.x == fromPoint.x && endPoint.z == fromPoint.z + z * 2)
                        ||
                        (
                            (!this.IsOutBoundary(checkPoint.x, checkPoint.z + z) && !_mapInfo[(int)checkPoint.x, (int)checkPoint.z + z])
                            || (!this.IsOutBoundary(checkPoint.x + x, checkPoint.z) && !_mapInfo[(int)checkPoint.x + x, (int)checkPoint.z])
                        )
                    )
                )
               )
            {
                Vector3 current = new(checkPoint.x, 0, checkPoint.z);
                this.AddPoint(checkMapPoint, current);
                return true;
            }

            return false;
        }

        //判断强迫邻居位置,决定斜向的扫描方向
        private void GetForcePointDirection(Vector3 checkPoint, int x, int z, MapPoint checkMapPoint, Vector3 fromPoint,
            bool isRecursivePoint,
            ref int xDirection,
            ref int zDirection)
        {
            //或者无父节点
            if (checkMapPoint.parent == null)
            {
                xDirection = x;
                zDirection = z;
            }
            else if (isRecursivePoint)
            {
                //递归的斜点只能沿着父节点的方向前进 
                int xDirectionTemp = checkPoint.x > fromPoint.x ? 1 : -1;
                int zDirectionTemp = checkPoint.z > fromPoint.z ? 1 : -1;

                xDirection = xDirectionTemp;
                zDirection = zDirectionTemp;
            }

            else
            {
                //前进阻挡在这里不用判断,作为 横/跳点 在加入时就应该做了阻挡判断,只需要确定,上下的障碍物在哪里就行,一个还是两个不确定
                int xDirectionTemp = checkPoint.x > fromPoint.x ? 1 : -1;
                int zDirectionTemp = checkPoint.z > fromPoint.z ? 1 : -1;

                if (fromPoint.z == checkPoint.z)
                {
                    //横跳点
                    //有障碍,返回强迫邻居的方向
                    if (xDirectionTemp == x
                        && !this.IsOutBoundary(checkPoint.x, checkPoint.z + z)
                        && _mapInfo[(int)checkPoint.x, (int)checkPoint.z + z])
                    {
                        xDirection = x;
                        zDirection = z;
                    }
                }

                else if (fromPoint.x == checkPoint.x)
                {
                    //竖跳点 
                    //有障碍,返回强迫邻居的方向
                    if (zDirectionTemp == z
                        && !this.IsOutBoundary(checkPoint.x + x, checkPoint.z)
                        && _mapInfo[(int)checkPoint.x + x, (int)checkPoint.z])
                    {
                        xDirection = x;
                        zDirection = z;
                    }
                }
                else if (xDirectionTemp == x && zDirectionTemp == z)
                {
                    //斜点沿着父对象的斜线检测/斜点存在的强迫邻居方向
                    xDirection = x;
                    zDirection = z;
                }
                else
                {
                    //剩下三个方向哪里有强迫邻居的斜跳点

                    //左右障碍
                    if (x == -xDirectionTemp
                        && !this.IsOutBoundary(checkPoint.x + x, checkPoint.z)
                        && _mapInfo[(int)checkPoint.x + x, (int)checkPoint.z])
                    {
                        xDirection = x;
                        zDirection = z;
                    }

                    //上下障碍
                    if (z == -zDirectionTemp
                        && !this.IsOutBoundary(checkPoint.x, checkPoint.z + z)
                        && _mapInfo[(int)checkPoint.x, (int)checkPoint.z + z])
                    {
                        xDirection = x;
                        zDirection = z;
                    }
                }
            }
        }

        /// <summary>
        /// jps垂直检测
        /// 退出的行为有两种,一种平移遇到障碍,一种遇到强迫邻居(这一种情况需要本斜点加入打开列表)
        /// 实际这个函数应该返回两个值,需不需要中断扫描/有没有找到强迫邻居
        /// </summary>
        /// <param name="zAddIndex">检测点z下标前进量</param>
        /// <param name="checkPoint">检测点</param>
        /// <param name="checkMapPoint">检测点包装类</param>
        /// <param name="zDirection">z轴方向</param>
        /// <param name="isRecursivePoint"></param>
        /// <param name="findObstacle">检测过程中是否发现强迫邻居</param>
        /// <returns>是否跳过, 跳过:true</returns>
        private bool VerticalStep(int zAddIndex, Vector3 checkPoint, MapPoint checkMapPoint, int zDirection,
            bool isRecursivePoint, ref bool findObstacle)
        {
            //当前点是否为障碍
            if (this.IsOutBoundary(checkPoint.x, zAddIndex) || _mapInfo[(int)checkPoint.x, zAddIndex])
                return true;

            //拓展点往上边走 当前点的左上角有障碍物 || 当前点的右上角有障碍物
            //拓展点往下边走 当前点的左下角有障碍物 || 当前点的右下角有障碍物
            //平移方向障碍物为终点,都将此点加入打开列表
            //(如果平移的下一个越界或者被障碍阻挡,也放弃)
            for (int xIndexOffset = -1; xIndexOffset <= 1; xIndexOffset++)
            {
                if (xIndexOffset == 0) continue;
                if (
                    (!this.IsOutBoundary(checkPoint.x, zAddIndex + zDirection)
                     && !_mapInfo[(int)checkPoint.x, zAddIndex + zDirection]
                     && !this.IsOutBoundary(checkPoint.x + xIndexOffset, zAddIndex)
                     && _mapInfo[(int)(checkPoint.x + xIndexOffset), zAddIndex]
                     && !this.IsOutBoundary(checkPoint.x + xIndexOffset, zAddIndex + zDirection)
                     && !_mapInfo[(int)(checkPoint.x + xIndexOffset), zAddIndex + zDirection])
                    || (endPoint.x == checkPoint.x && endPoint.z == zAddIndex + 0.5F)
                )
                {
                    if (!isRecursivePoint)
                    {
                        Vector3 current = new(checkPoint.x, 0, zAddIndex + 0.5F);

                        this.AddPoint(checkMapPoint, current);
                    }

                    findObstacle = true;

                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// jps水平检测
        /// 退出的行为有两种,一种平移遇到障碍,一种遇到强迫邻居(这一种情况需要本斜点加入打开列表)
        /// 实际这个函数应该返回两个值,需不需要中断扫描/有没有找到强迫邻居
        /// </summary>
        /// <param name="xAddIndex">检测点x下标前进量</param>
        /// <param name="checkPoint">检测点</param>
        /// <param name="checkMapPoint">检测点包装类</param>
        /// <param name="xDirection">x轴方向</param>
        /// <param name="isRecursivePoint"></param>
        /// <param name="findObstacle">检测过程中是否发现强迫邻居</param>
        /// <returns>是否跳过, 跳过:true</returns>
        private bool HorizontalStep(int xAddIndex, Vector3 checkPoint, MapPoint checkMapPoint, int xDirection,
            bool isRecursivePoint, ref bool findObstacle)
        {
            //当前点是否为障碍
            if (this.IsOutBoundary(xAddIndex, checkPoint.z) || _mapInfo[xAddIndex, (int)checkPoint.z])
                return true;

            //拓展点往左边走
            //当前点的上面有障碍物/左上角无障碍物 || 当前点的下面有障碍物/左下角无障碍物

            //拓展点往右边走
            //当前点的上面有障碍物/右上角无障碍物 || 当前点的下面有障碍物/由下角无障碍物

            //(如果平移的下一个越界或者被障碍阻挡,也放弃)
            //平移方向障碍物为终点,都将此点加入打开列表
            for (int zIndexOffset = -1; zIndexOffset <= 1; zIndexOffset++)
            {
                if (zIndexOffset == 0) continue;

                if (
                    (!this.IsOutBoundary(xAddIndex + xDirection, checkPoint.z)
                     && !_mapInfo[xAddIndex + xDirection, (int)checkPoint.z]
                     && !this.IsOutBoundary(xAddIndex, checkPoint.z + zIndexOffset)
                     && _mapInfo[xAddIndex, (int)checkPoint.z + zIndexOffset]
                     && !this.IsOutBoundary(xAddIndex + xDirection, checkPoint.z + zIndexOffset)
                     && !_mapInfo[xAddIndex + xDirection, (int)checkPoint.z + zIndexOffset]
                    )
                    || (endPoint.x == xAddIndex + 0.5F && endPoint.z == checkPoint.z)
                )
                {
                    //不是递归中的点,扫到强迫邻居/终点,需要立刻添加打开列表
                    if (!isRecursivePoint)
                    {
                        Vector3 current = new(xAddIndex + 0.5F, 0, checkPoint.z);

                        this.AddPoint(checkMapPoint, current);
                    }

                    findObstacle = true;

                    return true;
                }
            }

            return false;
        }

        private void AddPoint(MapPoint parentPoint, Vector3 current)
        {
            wantToNextPoint = current;

            if (!closeSet.Contains(current))
            {
                //如果这里不允许重复的打开列表点,就是在尝试更多的最佳路径
                if (!this.optimalPathStatus && openSet.Contains(current))
                {
                    //已经在开启列表中,跳过
                }
                else
                {
                    MapPoint newPoint = new(parentPoint, current, endPoint);
                    openSet.Add(current);
                    openList.Add(newPoint);
                    showData.Add(newPoint);
                }
            }

            if (this.stepStatus)
                Thread.Sleep(300);
        }

        //A*寻路
        IEnumerator AStarFindPath()
        {
            //数据清理
            openSet.Clear();
            openList.Clear();
            closeSet.Clear();
            path.Clear();
            showData.Clear();
            checkingPoint = Vector3.zero;
            checkingPointParent = Vector3.zero;
            wantToNextPoint = Vector3.zero;


            MapPoint start = new(null, startPoint, endPoint);
            openSet.Add(startPoint);
            openList.Add(start);

            MapPointComparer mapPointComparer = new(endPoint);
            MapPoint result = null;

            while (true)
            {
                if (openList.Count == 0)
                    break; //死路

                //1.从开启列表中找最佳点位
                openList.Sort(mapPointComparer);
                MapPoint checkMapPoint = openList[0]; //排序
                Vector3 checkPoint = checkMapPoint.position;

                //该点位退出开启列,进入关闭列表
                openList.Remove(checkMapPoint);
                openSet.Remove(checkPoint);
                closeSet.Add(checkPoint);

                if (checkPoint == endPoint)
                {
                    result = checkMapPoint;
                    break; //找到终点
                }

                checkingPoint = checkPoint;
                if (checkMapPoint.parent != null)
                    checkingPointParent = checkMapPoint.parent.position;

                //2.点位继续拓宽方向,不在关闭列表中的进入开启列表,将拓展点位父字点设置自己,
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0) continue; //这个点是自己

                        if (checkMapPoint.parent != null)
                        {
                            MapPoint parent = checkMapPoint.parent;

                            //利用jps的look ahead优化 A* 的路线(避免在一些转角走歪路)

                            //拓展节点在父节点的右上角
                            if (parent.position.x < checkPoint.x && parent.position.z < checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x, parent.position.z + 1)
                                    && _mapInfo[(int)parent.position.x, (int)parent.position.z + 1] && (x == -1 && z == 1))
                                {
                                    //父节点上方有障碍物,左上角可以加入扫描
                                }
                                else if (!this.IsOutBoundary(parent.position.x + 1, parent.position.z)
                                         && _mapInfo[(int)parent.position.x + 1, (int)parent.position.z] && (x == 1 && z == -1))
                                {
                                    //父节点右边有障碍物,右下角可以加入扫描
                                }
                                else if (x >= 0 && z >= 0)
                                {
                                    //拓展节点为右上角,只能拓展右上角三个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展节点在父节点的右下角
                            if (parent.position.x < checkPoint.x && parent.position.z > checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x, parent.position.z - 1)
                                    && _mapInfo[(int)parent.position.x, (int)parent.position.z - 1]
                                    && (x == -1 && z == -1))
                                {
                                    //父节点下方有障碍物,左下角可以加入扫描
                                }
                                else if (!this.IsOutBoundary(parent.position.x + 1, parent.position.z)
                                         && _mapInfo[(int)parent.position.x + 1, (int)parent.position.z]
                                         && (x == 1 && z == 1))
                                {
                                    //父节点右边有障碍物,右上角可以加入扫描
                                }
                                else if (x >= 0 && z <= 0)
                                {
                                    //拓展节点为右下角,只能拓展右下角三个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展节点在父节点的左上角
                            if (parent.position.x > checkPoint.x && parent.position.z < checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x, parent.position.z + 1)
                                    && _mapInfo[(int)parent.position.x, (int)parent.position.z + 1]
                                    && (x == 1 && z == 1))
                                {
                                    //父节点上方有障碍物,右上角可以加入扫描
                                }
                                else if (!this.IsOutBoundary(parent.position.x - 1, parent.position.z)
                                         && _mapInfo[(int)parent.position.x - 1, (int)parent.position.z]
                                         && (x == -1 && z == -1))
                                {
                                    //父节点左边有障碍物,左下角可以加入扫描
                                }
                                else if (x <= 0 && z >= 0)
                                {
                                    //拓展节点为左上角,只能拓展左上角三个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展节点在父节点的左下角
                            if (parent.position.x > checkPoint.x && parent.position.z > checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x - 1, parent.position.z)
                                    && _mapInfo[(int)parent.position.x - 1, (int)parent.position.z]
                                    && (x == -1 && z == 1))
                                {
                                    //父节点左边有障碍物,左上角可以加入扫描
                                }
                                else if (!this.IsOutBoundary(parent.position.x, parent.position.z - 1)
                                         && _mapInfo[(int)parent.position.x, (int)parent.position.z - 1]
                                         && (x == 1 && z == -1))
                                {
                                    //父节点下方有障碍物,右下角可以加入扫描
                                }
                                else if (x <= 0 && z <= 0)
                                {
                                    //拓展节点为左下角,只能拓展左下角三个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展点在父节点上方
                            if (parent.position.x == checkPoint.x && parent.position.z < checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x - 1, parent.position.z + 1)
                                    && _mapInfo[(int)parent.position.x - 1, (int)parent.position.z + 1]
                                    && (x == -1 && z == 1))
                                {
                                    //父节点的左上角有障碍物,那个点为需要加入考察
                                }
                                else if (!this.IsOutBoundary(parent.position.x + 1, parent.position.z + 1)
                                         && _mapInfo[(int)parent.position.x + 1, (int)parent.position.z + 1]
                                         && (x == 1 && z == 1))
                                {
                                    //父节点的右上角有障碍物,那个点为需要加入考察
                                }
                                else if (x == 0 && z == 1)
                                {
                                    //只需要往前看一个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展点在父节点下方
                            if (parent.position.x == checkPoint.x && parent.position.z > checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x - 1, parent.position.z - 1)
                                    && _mapInfo[(int)parent.position.x - 1, (int)parent.position.z - 1]
                                    && (x == -1 && z == -1))
                                {
                                    //父节点的左下角有障碍物,那个点为需要加入考察
                                }
                                else if (!this.IsOutBoundary(parent.position.x + 1, parent.position.z - 1)
                                         && _mapInfo[(int)parent.position.x + 1, (int)parent.position.z - 1]
                                         && (x == 1 && z == -1))
                                {
                                    //父节点的右下角有障碍物,那个点为需要加入考察
                                }
                                else if (x == 0 && z == -1)
                                {
                                    //只需要往前看一个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展点在父节点右边
                            if (parent.position.x < checkPoint.x && parent.position.z == checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x + 1, parent.position.z + 1)
                                    && _mapInfo[(int)parent.position.x + 1, (int)parent.position.z + 1]
                                    && (x == 1 && z == 1))
                                {
                                    //父节点的右上角有障碍物,那个点为需要加入考察
                                }
                                else if (!this.IsOutBoundary(parent.position.x + 1, parent.position.z - 1)
                                         && _mapInfo[(int)parent.position.x + 1, (int)parent.position.z - 1]
                                         && (x == 1 && z == -1))
                                {
                                    //父节点的右下角有障碍物,那个点为需要加入考察
                                }
                                else if (x == 1 && z == 0)
                                {
                                    //只需要往前看一个点
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            //拓展点在父节点左边
                            if (parent.position.x > checkPoint.x && parent.position.z == checkPoint.z)
                            {
                                if (!this.IsOutBoundary(parent.position.x - 1, parent.position.z + 1)
                                    && _mapInfo[(int)parent.position.x - 1, (int)parent.position.z + 1]
                                    && (x == -1 && z == 1))
                                {
                                    //父节点的左上角有障碍物,那个点为需要加入考察
                                }
                                else if (!this.IsOutBoundary(parent.position.x - 1, parent.position.z - 1)
                                         && _mapInfo[(int)parent.position.x - 1, (int)parent.position.z - 1]
                                         && (x == -1 && z == -1))
                                {
                                    //父节点的左下角有障碍物,那个点为需要加入考察
                                }
                                else if (x == -1 && z == 0)
                                {
                                    //只需要往前看一个点
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }


                        //拓展点,边界判断
                        if (this.IsOutBoundary(checkPoint.x + x, checkPoint.z + z))
                            continue;

                        //为对角点,当前点去对角线是否有阻碍
                        if (x != 0 && z != 0)
                        {
                            //右上角
                            if ((x > 0 && z > 0)
                                && _mapInfo[(int)checkPoint.x, (int)checkPoint.z + 1]
                                && _mapInfo[(int)checkPoint.x + 1, (int)checkPoint.z])
                                continue;
                            //右下角
                            if ((x > 0 && z < 0)
                                && _mapInfo[(int)checkPoint.x + 1, (int)checkPoint.z]
                                && _mapInfo[(int)checkPoint.x, (int)checkPoint.z - 1])
                                continue;

                            //左上角
                            if ((x < 0 && z > 0)
                                && _mapInfo[(int)checkPoint.x, (int)checkPoint.z + 1]
                                && _mapInfo[(int)checkPoint.x - 1, (int)checkPoint.z])
                                continue;

                            //左下角
                            if ((x < 0 && z < 0)
                                && _mapInfo[(int)checkPoint.x, (int)checkPoint.z - 1]
                                && _mapInfo[(int)checkPoint.x - 1, (int)checkPoint.z])
                                continue;
                        }


                        if (_mapInfo[(int)checkPoint.x + x, (int)checkPoint.z + z])
                            continue; //在障碍列表(地图数据生成),跳过

                        Vector3 current = new(checkPoint.x + x, 0, checkPoint.z + z);
                        wantToNextPoint = current;

                        if (closeSet.Contains(current))
                            continue; //在关闭列表中,跳过

                        //如果这里不允许重复的打开列表点,就是在尝试更多的最佳路径
                        if (!this.optimalPathStatus && openSet.Contains(current))
                        {
                            continue; //已经在开启列表中,跳过
                        }

                        MapPoint newPoint = new(checkMapPoint, current, endPoint);
                        openSet.Add(current);
                        openList.Add(newPoint);
                        showData.Add(newPoint);

                        if (this.stepStatus)
                        {
                            yield return this._stepSecond;
                        }
                    }
                }

                //4.回到步骤1


                //ui调试

                yield return _waitForSeconds;

                while (this.stopStatus)
                {
                    yield return this._stopSecond;
                }

                if (this.stepStatus)
                {
                    this.stopStatus = !this.stopStatus;
                }
            }


            if (result != null)
            {
                MapPoint tempPoint = result;
                //确定路线,从终点往回遍历链表
                path.Add(result.position);
                while (true)
                {
                    tempPoint = tempPoint.parent;
                    if (tempPoint == null) break;
                    path.Add(tempPoint.position);
                }

                print("寻路完成..");
            }
            else
            {
                print("死路...");
            }
        }


        //边界检查,返回true,越界 x可能区间[0.5,9.5] 得 0<=x<10
        bool IsOutBoundary(float x, float z)
        {
            return !(0 <= x && x < meshSize && 0 <= z && z < meshSize);
            // return x < 0 || x > meshSize - 1 || z < 0 || z > meshSize - 1; 
        }

        private void Start()
        {
            AStarUtils aStarUtils = new();
            JPSUtils jpsUtils = new(useThetaMode: true);

            this.removeObstacle.onValueChanged.AddListener((res) => { this.selectIndex = res ? -1 : 0; });
            this.obstacle.onValueChanged.AddListener((res) => { this.selectIndex = res ? 1 : 0; });

            this.start.onValueChanged.AddListener((res) => { this.selectIndex = res ? 2 : 0; });
            this.end.onValueChanged.AddListener((res) => { this.selectIndex = res ? 3 : 0; });
            this.dataModel.onValueChanged.AddListener((res) => { this.selectIndex = res ? 4 : 0; });
            this.calculate.onClick.AddListener(() =>
            {
                //开始计算
                if (endPoint != startPoint)
                {
                    if (this.selectFindType == 2)
                    {
                        StartCoroutine(this.JPSFindPath());
                        // path = jpsUtils.GetPath(startPoint, endPoint, _mapInfo, meshSize); //测试封装
                    }
                    else if (this.selectFindType == 3)
                    {
                        StartCoroutine(this.JPSFindPath());
                    }
                    else
                    {
                        StartCoroutine(this.AStarFindPath());

                        // path = aStarUtils.GetPath(startPoint, endPoint,_mapInfo, meshSize);//测试封装
                    }
                }
                else
                {
                    print("脚下即远方,终点即起点");
                }
            });
            this.stop.onClick.AddListener(() =>
            {
                stopStatus = !stopStatus;

                if (stopStatus)
                {
                    if (!this.stepStatus)
                    {
                        Image image = this.stop.GetComponent<Image>();
                        image.color = Color.red;
                        Text text = this.stop.transform.GetChild(0).GetComponent<Text>();
                        text.text = "resume";
                    }
                }
                else
                {
                    if (!this.stepStatus)
                    {
                        Image image = this.stop.GetComponent<Image>();
                        image.color = Color.white;
                        Text text = this.stop.transform.GetChild(0).GetComponent<Text>();
                        text.text = "stop";
                    }
                }
            });
            this.step.onClick.AddListener(() =>
            {
                this.stepStatus = !this.stepStatus;
                if (this.stepStatus)
                {
                    Image image = this.step.GetComponent<Image>();
                    image.color = Color.cyan;


                    Text text = this.stop.transform.GetChild(0).GetComponent<Text>();
                    text.text = "next";

                    Image image2 = this.stop.GetComponent<Image>();
                    image2.color = Color.white;
                }
                else
                {
                    Image image = this.step.GetComponent<Image>();
                    image.color = Color.white;
                    this.stop.transform.localScale = Vector3.one;
                    if (stopStatus)
                    {
                        Image image2 = this.stop.GetComponent<Image>();
                        image2.color = Color.red;
                        Text text = this.stop.transform.GetChild(0).GetComponent<Text>();
                        text.text = "resume";
                    }
                    else
                    {
                        Image image2 = this.stop.GetComponent<Image>();
                        image2.color = Color.white;
                        Text text = this.stop.transform.GetChild(0).GetComponent<Text>();
                        text.text = "stop";
                    }
                }
            });

            this.slider.onValueChanged.AddListener((value) => { this._waitForSeconds = new WaitForSeconds(value); });

            this.reset.onClick.AddListener(() =>
            {
                obstacleSet.Clear();
                _mapInfo = new bool[meshSize, meshSize];

                path.Clear();
                openSet.Clear();
                openList.Clear();
                closeSet.Clear();
                checkingPoint = Vector3.zero;
                wantToNextPoint = Vector3.zero;

                startPoint = Vector3.zero;
                endPoint = Vector3.zero;

                showData.Clear();
            });
            this.clearGraph.onClick.AddListener(() =>
            {
                path.Clear();
                openSet.Clear();
                openList.Clear();
                closeSet.Clear();
                checkingPoint = Vector3.zero;
                wantToNextPoint = Vector3.zero;

                showData.Clear();
            });

            this.aStar.onValueChanged.AddListener((res) =>
            {
                if (res) this.selectFindType = 1;
            });
            this.JPS.onValueChanged.AddListener((res) =>
            {
                if (res) this.selectFindType = 2;
            });
            this.thetaStar.onValueChanged.AddListener((res) =>
            {
                if (res) this.selectFindType = 3;
            });


            this.optimalPathStatusCalculate.onValueChanged.AddListener((res) => { this.optimalPathStatus = res; });
        }

        private bool animation = false;

        private void Update()
        {
            this.Graph();
            if (this.stepStatus)
            {
                if (animation)
                {
                    this.stop.transform.localScale += (Vector3.one * 0.1F * Time.deltaTime);
                    if (this.stop.transform.localScale.magnitude >= Vector3.one.magnitude)
                        animation = !animation;
                }
                else
                {
                    this.stop.transform.localScale -= (Vector3.one * 0.1F * Time.deltaTime);
                    if (this.stop.transform.localScale.magnitude <= (Vector3.one * 0.85F).magnitude)
                        animation = !animation;
                }
            }
        }


        //画网格点
        void Graph()
        {
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hitInfo, 1000F, 1 << 3))
                {
                    //print(hitInfo.point);
                    Vector3 temp = new();
                    temp.x = Mathf.Floor(hitInfo.point.x) + 0.5F;
                    temp.z = Mathf.Floor(hitInfo.point.z) + 0.5F;
                    if (this.selectIndex == 1)
                    {
                        obstacleSet.Add(temp);
                        _mapInfo[(int)Mathf.Floor(hitInfo.point.x), (int)Mathf.Floor(hitInfo.point.z)] = true;
                    }
                    else if (this.selectIndex == -1)
                    {
                        obstacleSet.Remove(temp);
                        _mapInfo[(int)Mathf.Floor(hitInfo.point.x), (int)Mathf.Floor(hitInfo.point.z)] = false;
                    }
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hitInfo, 1000F, 1 << 3))
                {
                    print(hitInfo.point);
                    Vector3 temp = new();
                    temp.x = Mathf.Floor(hitInfo.point.x) + 0.5F;
                    temp.z = Mathf.Floor(hitInfo.point.z) + 0.5F;
                    if (this.selectIndex == 1)
                    {
                        obstacleSet.Add(temp);
                        _mapInfo[(int)Mathf.Floor(hitInfo.point.x), (int)Mathf.Floor(hitInfo.point.z)] = true;
                    }
                    else if (this.selectIndex == 2)
                    {
                        if (obstacleSet.Contains(temp))
                            print("起点为障碍物..");
                        else
                            startPoint = temp;
                    }
                    else if (this.selectIndex == 3)
                    {
                        if (obstacleSet.Contains(temp))
                            print("终点为障碍物..");
                        else
                            endPoint = temp;
                    }

                    else if (this.selectIndex == 4)
                    {
                        string msg = "当前点位数据:\r\n";
                        foreach (MapPoint mapPoint in showData)
                        {
                            if (mapPoint.position.x == temp.x && mapPoint.position.z == temp.z)
                            {
                                msg += $"F={mapPoint.fValue},\r\nH={mapPoint.hValue},\r\nG={mapPoint.gValue}\r\n";
                                if (mapPoint.parent != null)
                                    msg += $"parent={Mathf.Floor(mapPoint.parent.position.x)},{Mathf.Floor(mapPoint.parent.position.z)}\r\n";
                            }
                        }

                        data.text = msg;
                    }
                    else if (this.selectIndex == -1)
                    {
                        obstacleSet.Remove(temp);
                        _mapInfo[(int)Mathf.Floor(hitInfo.point.x), (int)Mathf.Floor(hitInfo.point.z)] = false;
                    }
                }
            }
        }

        //辅助线
        void OnDrawGizmos()
        {
            GraphMesh();

            //障碍
            if (obstacleSet != null && obstacleSet.Count > 0)
            {
                Gizmos.color = Color.red;
                foreach (Vector3 vector3 in obstacleSet)
                {
                    Gizmos.DrawCube(vector3, new Vector3(1F, 0.1F, 1F));
                }
            }

            //打开列表
            if (openList != null && openList.Count > 0)
            {
                Gizmos.color = Color.gray;
                foreach (MapPoint item in openList)
                {
                    Gizmos.DrawCube(item.position, new Vector3(1F, 0.1F, 1F));
                }
            }

            //关闭列表
            if (closeSet != null && closeSet.Count > 0)
            {
                Gizmos.color = Color.black;
                foreach (Vector3 vector3 in closeSet)
                {
                    Gizmos.DrawCube(vector3, new Vector3(1F, 0.1F, 1F));
                }
            }

            //路线
            if (path is { Count: > 0 })
            {
                foreach (Vector3 vector3 in path)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(vector3, new Vector3(1F, 0.1F, 1F));
                }
            }


            //起点
            if (startPoint != Vector3.zero)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawCube(startPoint, new Vector3(1F, 0.1F, 1F));
            }

            //终点
            if (endPoint != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(endPoint, new Vector3(1F, 0.1F, 1F));
            }

            //debug点位
            if (path != null && path.Count == 0 && checkingPoint != Vector3.zero && wantToNextPoint != Vector3.zero)
            {
                Gizmos.color = Color.green;
                if (checkingPointParent != Vector3.zero)
                {
                    Gizmos.DrawLine(checkingPointParent, checkingPoint);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawCube(checkingPointParent, new Vector3(0.3F, 0.1F, 0.3F));
                }

                Gizmos.color = Color.green;

                Gizmos.DrawLine(checkingPoint, wantToNextPoint);
            }

            //debug路线
            if (path != null && path.Count > 0)
            {
                Vector3 last = path[0];
                foreach (Vector3 vector3 in path)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(last, vector3);
                    last = vector3;
                }
            }

            //debugjps路线
            if (this.selectFindType == 2)
            {
                foreach (MapPoint aMapPoint in showData)
                {
                    Gizmos.color = Color.red;
                    if (aMapPoint.parent != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(aMapPoint.parent.position, aMapPoint.position);
                    }
                }
            }
        }

        //自定义比较器
        class MyComparer : IComparer<Vector3>
        {
            public int Compare(Vector3 v1, Vector3 v2)
            {
                //这里的 consume 根据距离父节点的距离,取点位的不同消耗值
                // relation.TryGetValue(v1, out Vector3 v1Parent);
                // relation.TryGetValue(v2, out Vector3 v2Parent);

                //曼哈顿距离
                float temp1 = Mathf.Abs(endPoint.x - v1.x) + Mathf.Abs(endPoint.z - v1.z);
                float temp2 = Mathf.Abs(endPoint.x - v2.x) + Mathf.Abs(endPoint.z - v2.z);
                return (int)(temp1 - temp2);

                //#  Diagnol distance
                /*float dx = Mathf.Abs(v1.x - v2.x);
                float dy = Mathf.Abs(v1.z - v2.z);
                float min_xy = Mathf.Min(dx, dy);
                float h = dx + dy + (Mathf.Sqrt(2) - 2) * min_xy;
                return (int)h;*/

                // #  Euclidean 欧几里得距离（欧氏距离）hypot
                // float h = Math.hypot(v1.x - v2.x, v1.z - v2.z);
                // return (int)h;
            }
        }
    }
}