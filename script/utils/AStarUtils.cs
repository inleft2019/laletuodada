using System.Collections.Generic;
using script.model;
using UnityEngine;

namespace script.utils
{
    /// <summary>
    /// A型寻路封装
    /// Creator@BiliBili 笔墨v
    /// since 202406
    /// </summary>
    public class AStarUtils
    {
       private bool[,] _mapInfo; //另一种表示地图障碍的方法
       private int _meshSize; //网格大小,改这个可以代表视野范围/搜索范围
       private Vector3 startPoint;
       private Vector3 endPoint;
       
       private MapPointComparer mapPointComparer;
       
       private bool optimalPathStatus;
       
       
       //打开列表,用于查重
       private HashSet<Vector3> openSet = new();
       
       //打开列表,启发函数值排序
       //private  SortedSet<MapPoint> openSortedSet = new(new MapPointComparer(endPoint));
       private List<MapPoint> openList = new();
       
       //关闭列表
       private HashSet<Vector3> closeSet = new();
       
       
       private List<Vector3> path = new(); //寻路路线 
       
       public AStarUtils(MapPointComparer comparer = null, bool optimalPath = false)
       {
           this.optimalPathStatus = optimalPath;
       
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
           this._mapInfo = map;
           this._meshSize = meshSize;
           this.mapPointComparer.SetEndpoint(endPoint);
           return this.FindPathAStar();
       }
       
       private bool IsOutBoundary(float x, float z)
       {
           return !(0 <= x && x < _meshSize && 0 <= z && z < _meshSize);
       }
       
       //寻路
       private List<Vector3> FindPathAStar()
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
       
               //该点位退出开启列,进入关闭列表
               this.openList.Remove(checkMapPoint);
               this.openSet.Remove(checkPoint);
               this.closeSet.Add(checkPoint);
       
               if (checkPoint == this.endPoint)
               {
                   result = checkMapPoint;
                   break; //找到终点
               }
       
               //2.点位继续拓宽方向,不在关闭列表中的进入开启列表,将拓展点位父字点设置自己,
               for (int x = -1; x <= 1; x++)
               {
                   for (int z = -1; z <= 1; z++)
                   {
                       if (x == 0 && z == 0) continue; //这个点是自己
       
                       if (checkMapPoint.parent != null)
                       {
                           MapPoint parent = checkMapPoint.parent;
       
                           if (this.UseJPSOptimal(parent, checkPoint, x, z)) continue;
                       }
       
                       //拓展点,边界判断
                       if (this.IsOutBoundary(checkPoint.x + x, checkPoint.z + z))
                           continue;
       
                       //为对角点,当前点去对角线是否有阻碍
                       if (x != 0 && z != 0)
                       {
                           if (CheckObstacle(x, z, checkPoint)) continue;
                       }
       
       
                       if (_mapInfo[(int)checkPoint.x + x, (int)checkPoint.z + z])
                           continue; //在障碍列表(地图数据生成),跳过
       
                       Vector3 current = new(checkPoint.x + x, 0, checkPoint.z + z);
       
                       if (this.closeSet.Contains(current))
                           continue; //在关闭列表中,跳过
       
                       //如果这里不允许重复的打开列表点,就是在尝试更多的最佳路径
                       if (!this.optimalPathStatus && this.openSet.Contains(current))
                       {
                           continue; //已经在开启列表中,跳过
                       }
       
                       MapPoint newPoint = new(checkMapPoint, current, this.endPoint);
                       this.openSet.Add(current);
                       this.openList.Add(newPoint);
                   }
               }
       
               //4.回到步骤1
           }
       
       
           if (result != null)
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
       
           return this.path;
       }
       
       /// <summary>
       /// 利用jps的look ahead优化 A* 的路线(避免在一些转角走歪路)
       /// 不是非必要计算的,只是可以让路线不在转角处拐弯,而是提前拐弯
       /// </summary>
       /// <param name="parent"></param>
       /// <param name="checkPoint"></param>
       /// <param name="x"></param>
       /// <param name="z"></param>
       /// <returns></returns>
       private bool UseJPSOptimal(MapPoint parent, Vector3 checkPoint, int x, int z)
       {
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
                   return true;
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
                   return true;
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
                   return true;
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
                   return true;
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
                   return true;
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
                   return true;
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
                   return true;
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
                   return true;
               }
           }
       
           return false;
       }
       
       /// <summary>
       /// 检测对角线障碍
       /// </summary>
       /// <param name="x"></param>
       /// <param name="z"></param>
       /// <param name="checkPoint"></param>
       /// <returns></returns>
       private bool CheckObstacle(int x, int z, Vector3 checkPoint)
       {
           //右上角
           if ((x > 0 && z > 0)
               && _mapInfo[(int)checkPoint.x, (int)checkPoint.z + 1]
               && _mapInfo[(int)checkPoint.x + 1, (int)checkPoint.z])
               return true;
           //右下角
           if ((x > 0 && z < 0)
               && _mapInfo[(int)checkPoint.x + 1, (int)checkPoint.z]
               && _mapInfo[(int)checkPoint.x, (int)checkPoint.z - 1])
               return true;
       
           //左上角
           if ((x < 0 && z > 0)
               && _mapInfo[(int)checkPoint.x, (int)checkPoint.z + 1]
               && _mapInfo[(int)checkPoint.x - 1, (int)checkPoint.z])
               return true;
       
           //左下角
           if ((x < 0 && z < 0)
               && _mapInfo[(int)checkPoint.x, (int)checkPoint.z - 1]
               && _mapInfo[(int)checkPoint.x - 1, (int)checkPoint.z])
               return true;
           return false;
       }
    }
}