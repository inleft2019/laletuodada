using System.Collections.Generic;
using UnityEngine;

namespace script.model
{
    /// <summary>
    /// 地图点位比较器
    /// </summary>
    public class MapPointComparer : IComparer<MapPoint>
    {
        private Vector3 _endPoint;

        public MapPointComparer(Vector3 endPoint)
        {
            this._endPoint = endPoint;
        }

        public void SetEndpoint(Vector3 endPoint)

        {
            this._endPoint = endPoint;
        }

        public int Compare(MapPoint v1, MapPoint v2)
        {
            //完全相同的点,重复元素
            if (v1.position.x == v2.position.x && v1.position.z == v2.position.z)
            {
                if (v1.parent != null && v2.parent != null && v1.parent.position == v2.parent.position)
                {
                    return 0;
                }
            }

            int result = v1.fValue.CompareTo(v2.fValue);

            //曼哈顿距离相同,找直线距离
             if (result == 0)
             {
                 return (Vector3.Distance(this._endPoint, v1.position).CompareTo(Vector3.Distance(this._endPoint, v2.position)));
             }

            return result;
        }
    }
}