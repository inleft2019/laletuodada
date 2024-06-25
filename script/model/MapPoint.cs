using UnityEngine;

namespace script.model
{
    public class MapPoint
    {
        
        public Vector3 position;
        public float gValue; //上一个节点g值+移动代价(两点直接距离)
        public float hValue; //曼哈顿距离
        public float fValue; //启发函数值结果:F=g+h
        public MapPoint parent; //父节点


        public void AttemptChangeParent(MapPoint newParent, Vector3 endPoint)
        {
            float gValueTemp = newParent.gValue + Vector3.Distance(newParent.position, this.position);
            if (this.fValue >= gValueTemp + this.hValue)
            {
                this.gValue = gValueTemp;
                this.fValue = gValueTemp + this.hValue;
                this.parent = newParent;
            }
        }

        /// <summary>
        /// 描述一个地图点
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="position">位置</param>
        /// <param name="endPoint">终点</param>
        public MapPoint(MapPoint parent, Vector3 position, Vector3 endPoint)
        {
            this.position = position;
            this.parent = parent;
            if (this.parent != null)
            {
                this.gValue = parent.gValue + Vector3.Distance(parent.position, position);

                this.hValue = Mathf.Abs(endPoint.x - position.x) + Mathf.Abs(endPoint.z - position.z);
            }
            else
            {
                this.gValue = 0;
                this.hValue = Mathf.Abs(endPoint.x - position.x) + Mathf.Abs(endPoint.z - position.z);
            }

            this.fValue = this.gValue + this.hValue; //构建启发函数值
        }
    }
}