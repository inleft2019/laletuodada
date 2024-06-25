using System.Collections.Generic;
using script.model;
using UnityEngine;

namespace script.utils
{
    /// <summary>
    /// JPS寻路封装
    /// Creator@BiliBili 笔墨v
    /// since 202406
    /// </summary>
    public class JPSUtils
    {
       private bool[,] _mapInfo; //另一种表示地图障碍的方法
       private int _meshSize; //网格大小,改这个可以代表视野范围/搜索范围
       private Vector3 startPoint;
       private Vector3 endPoint;
       
       private MapPointComparer mapPointComparer;
       
       private bool optimalPathStatus;
       
       private bool useThetaMode; //路径平滑处理
       
       //打开列表,用于查重
       private HashSet<Vector3> openSet = new();
       
       //打开列表,启发函数值排序
       //private static SortedSet<MapPoint> openSortedSet = new(new MapPointComparer());
       private List<MapPoint> openList = new();
       
       //关闭列表
       private HashSet<Vector3> closeSet = new();
       
       
       private List<Vector3> path = new(); //寻路路线 
       
       public JPSUtils(MapPointComparer comparer = null, bool optimalPath = false, bool useThetaMode = false)
       {
           this.optimalPathStatus = optimalPath;
           this.useThetaMode = useThetaMode;
       
           if (comparer != null)
               this.mapPointComparer = comparer;
           else
               this.mapPointComparer = new MapPointComparer(Vector3.zero);
       }
       
       //获取路线
       public List<Vector3> GetPath(Vector3 startPoint, Vector3 endPoint, bool[,] map, int meshSize)
       {
           this.startPoint = startPoint;
           this.endPoint = endPoint;
           _mapInfo = map;
           _meshSize = meshSize;
           this.mapPointComparer.SetEndpoint(endPoint);
           return this.FindPathJPS();
       }
       
       //jps寻路
       private List<Vector3> FindPathJPS()
       {
           //数据清理
           this.openSet.Clear();
           this.openList.Clear();
           this.closeSet.Clear();
           this.path.Clear();
       
       
           MapPoint start = new(null, this.startPoint, this.endPoint);
           this.openSet.Add(this.startPoint);
           this.openList.Add(start);
       
           MapPoint result = null;
       
           while (true)
           {
               if (this.openList.Count == 0)
                   break; //死路
       
               //1.从开启列表中找最佳点位
               this.openList.Sort(this.mapPointComparer);
               MapPoint checkMapPoint = this.openList[0]; //排序
               Vector3 checkPoint = checkMapPoint.position;
       
               Vector3 parentPoint = Vector3.one * -1;
               if (checkMapPoint.parent != null)
                   parentPoint = checkMapPoint.parent.position;
       
               //该点位退出开启列,进入关闭列表
               this.openList.Remove(checkMapPoint);
               this.openSet.Remove(checkPoint);
               this.closeSet.Add(checkPoint);
       
               if (checkPoint == this.endPoint)
               {
                   result = checkMapPoint;
                   break; //找到终点
               }
       
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
           }
       
           if (result != null)
           {
               if (this.useThetaMode)
               {
                   RayTestObstacle(result, result.parent);
               }
               else
               {
                   MapPoint tempPoint = result;
                   //确定路线,从终点往回遍历链表
                   this.path.Add(result.position);
                   while (true)
                   {
                       tempPoint = tempPoint.parent;
                       if (tempPoint == null) break;
                       this.path.Add(tempPoint.position);
                   }
               }
           }
       
           return this.path;
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
           if (this.endPoint.x == checkPoint.x && this.endPoint.z == checkPoint.z)
           {
               this.AddPoint(checkMapPoint, this.endPoint);
               return;
           }
       
           //如果是空父结点,横向扫描时,需要立刻把强迫邻居加入打开列表
           //有父节点的,检测到强迫邻居,应该退出,任意横竖向有强迫邻居,本斜点加入打开列表
           //斜点进行本轮的横向检测
           bool xResult = false;
           bool zResult = false;
       
       
           if (checkMapPoint.parent != null || isRecursivePoint || xDirection * zDirection == 1)
           {
               for (int xAddIndex = (int)checkPoint.x + xDirection; xDirection > 0 ? (xAddIndex < _meshSize) : xAddIndex >= 0;)
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
               for (int zAddIndex = (int)checkPoint.z + zDirection; zDirection > 0 ? (zAddIndex < _meshSize) : zAddIndex >= 0;)
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
       
           if (this.closeSet.Contains(next))
               return; //在关闭列表中,跳过
       
           //如果这里不允许重复的打开列表点,就是在尝试更多的最佳路径
           if (!this.optimalPathStatus && this.openSet.Contains(next))
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
                       (this.endPoint.x == fromPoint.x + x * 2 && this.endPoint.z == fromPoint.z)
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
                       (this.endPoint.x == fromPoint.x && this.endPoint.z == fromPoint.z + z * 2)
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
       /// jps垂直检测(和水平检测逻辑一致,镜像xz轴)
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
                   || (this.endPoint.x == checkPoint.x && this.endPoint.z == zAddIndex + 0.5F)
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
       /// jps水平检测(和垂直检测逻辑一致,镜像xz轴)
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
                   || (this.endPoint.x == xAddIndex + 0.5F && this.endPoint.z == checkPoint.z)
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
           if (!this.closeSet.Contains(current))
           {
               //如果这里不允许重复的打开列表点,就是在尝试更多的最佳路径
               if (!this.optimalPathStatus && this.openSet.Contains(current))
               {
                   //已经在开启列表中,跳过
               }
               else
               {
                   MapPoint newPoint = new(parentPoint, current, this.endPoint);
                   this.openSet.Add(current);
                   this.openList.Add(newPoint);
               }
           }
       }
       
       //边界检查,返回true,越界 x可能区间[0.5,9.5] 得 0<=x<10
       private bool IsOutBoundary(float x, float z)
       {
           return !(0 <= x && x < _meshSize && 0 <= z && z < _meshSize);
       }
       
       //超过三个点才需要进来判断  
       //每个格子和祖父节点相连判断
       //两格子之间之间没有障碍,则继续判断祖父节点的父节点,一直到遇到障碍或者到尽头,再和最后一次格子直接相连
       //新连的格子不是尽头,回开头,继续这个过程
       private void RayTestObstacle(MapPoint mapPoint, MapPoint nextPoint)
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
       
           if (!MyGridHelper.GetTouchedPosBetweenTwoPoints(start.position, end.parent.position, _mapInfo))
           {
               //没有阻挡,下一位..
               return this.Recusive(start, end.parent);
           }
       
           return end;
       }
    }
}