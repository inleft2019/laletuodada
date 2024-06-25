using UnityEngine;

namespace script.utils
{
    public class MyGridHelper
    {
        /// <summary>
        /// 计算两点间经过的格子有无障碍
        /// 返回: 有障碍:true
        /// </summary>
        public static bool GetTouchedPosBetweenTwoPoints(Vector3 v1, Vector3 v2, bool[,] mapInfo)
        {
            Vector2Int to = new((int)v2.x, (int)v2.z);
            Vector2Int from = new((int)v1.x, (int)v1.z);
            return GetTouchedPosBetweenOrigin2Target(to - from, from, mapInfo);
        }

        /// <summary>
        /// 镜像翻转 交换 X Y
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private static void GetSteepPosition(ref int x, ref int y)
        {
            x = x ^ y;
            y = x ^ y;
            x = x ^ y;
        }

        /// <summary>
        /// 计算目标位置到原点所经过的格子
        /// </summary>
        static bool GetTouchedPosBetweenOrigin2Target(Vector2Int target, Vector2Int offset, bool[,] mapInfo)
        {
            bool steep = Mathf.Abs(target.y) > Mathf.Abs(target.x);
            int x = steep ? target.y : target.x;
            int y = steep ? target.x : target.y;

            //斜率
            float tangent = (float)y / x;

            float delta = x > 0 ? 0.5f : -0.5f;

            for (int i = 1; i < 2 * Mathf.Abs(x); i++)
            {
                float tempX = i * delta;
                float tempY = tangent * tempX;
                bool isOnEdge = Mathf.Abs(tempY - Mathf.FloorToInt(tempY)) == 0.5f;
                int tempIntX;
                int tempIntY;

                //偶数 格子内部判断
                if ((i & 1) == 0)
                {
                    //在边缘,则上下两个格子都满足条件
                    if (isOnEdge)
                    {
                        tempIntX = Mathf.RoundToInt(tempX);
                        tempIntY = Mathf.CeilToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;

                        tempIntX = Mathf.RoundToInt(tempX);
                        tempIntY = Mathf.FloorToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;
                    }
                    //不在边缘就所处格子满足条件
                    else
                    {
                        tempIntX = Mathf.RoundToInt(tempX);
                        tempIntY = Mathf.RoundToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;
                    }
                }

                //奇数 格子边缘判断
                else
                {
                    //在格子交点处,不视为阻挡,忽略
                    //改:在格子交点处,上下左右四个格子都需要判断
                    if (isOnEdge)
                    {
                        tempIntX = Mathf.RoundToInt(tempX);
                        tempIntY = Mathf.CeilToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;

                        tempIntX = Mathf.RoundToInt(tempX);
                        tempIntY = Mathf.FloorToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;


                        tempIntX = Mathf.CeilToInt(tempX);
                        tempIntY = Mathf.RoundToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;

                        tempIntX = Mathf.FloorToInt(tempX);
                        tempIntY = Mathf.RoundToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;
                    }
                    //否则左右两个格子满足
                    else
                    {
                        tempIntX = Mathf.CeilToInt(tempX);
                        tempIntY = Mathf.RoundToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;

                        tempIntX = Mathf.FloorToInt(tempX);
                        tempIntY = Mathf.RoundToInt(tempY);
                        if (steep)
                            GetSteepPosition(ref tempIntX, ref tempIntY); //镜像翻转 交换 X Y
                        tempIntX += offset.x;
                        tempIntY += offset.y;
                        if (mapInfo[tempIntX, tempIntY])
                            return true;
                    }
                }
            }

            return false;
        }
    }
}