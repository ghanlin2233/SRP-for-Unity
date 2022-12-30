using UnityEngine;

/// <summary>
///
/// </summary>
public class CSM
{
    public float[] depthDivide = { 0.07f, 0.13f, 0.25f, 0.55f };
    public float[] orthoWidth = new float[4];

    //主相机远近平面
    Vector3[] nearPlane = new Vector3[4];
    Vector3[] farPlane = new Vector3[4];

    Vector3[] near01 = new Vector3[4], far01 = new Vector3[4];
    Vector3[] near02 = new Vector3[4], far02 = new Vector3[4];
    Vector3[] near03 = new Vector3[4], far03 = new Vector3[4];
    Vector3[] near00 = new Vector3[4], far00 = new Vector3[4];

    Vector3[] box1, box2, box3, box0;

    struct MainCameraSettings
    {
        public Vector3 position;
        public Quaternion rotation;
        public float nearClipPlane;
        public float farClipPlane;
        public float aspect;
    };
    MainCameraSettings settings;


    // 齐次坐标矩阵乘法变换
    Vector3 MatTransform(Matrix4x4 m, Vector3 v, float w)
    {
        Vector4 v4 = new Vector4(v.x, v.y, v.z, w);
        v4 = m * v4;
        return new Vector3(v4.x, v4.y, v4.z);
    }

    // 计算光源方向包围盒的世界坐标
    Vector3[] LightSpaceAABB(Vector3[] nearCorners, Vector3[] farCorners, Vector3 lightDir)
    {
        var shadowViewInv = Matrix4x4.LookAt(Vector3.zero, lightDir, Vector3.up);
        var shadowView = shadowViewInv.inverse;

        //视锥体顶点转光源方向
        for (int i = 0; i < 4; i++)
        {
            nearCorners[i] = MatTransform(shadowView, nearCorners[i], 1.0f);
            farCorners[i] = MatTransform(shadowView, farCorners[i], 1.0f);
        }

        //计算AABB包围盒
        float[] x = new float[8];
        float[] y = new float[8];
        float[] z = new float[8];
        for (int i = 0; i < 4; i++)
        {
            x[i] = nearCorners[i].x; x[i + 4] = farCorners[i].x;
            y[i] = nearCorners[i].y; y[i + 4] = farCorners[i].y;
            z[i] = nearCorners[i].z; z[i + 4] = farCorners[i].z;
        }
        float xmin = Mathf.Min(x), xmax = Mathf.Max(x);
        float ymin = Mathf.Min(y), ymax = Mathf.Max(y);
        float zmin = Mathf.Min(z), zmax = Mathf.Max(z);

        Vector3[] points =
        {
            new Vector3(xmin, ymin, zmin), new Vector3(xmin, ymin, zmax), new Vector3(xmin, ymax, zmin), new Vector3(xmin, ymax, zmax),
            new Vector3(xmax, ymin, zmin), new Vector3(xmax, ymin, zmax), new Vector3(xmax, ymax, zmin), new Vector3(xmax, ymax, zmax)
        };
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = MatTransform(shadowViewInv, points[i], 1.0f);
        }

        //光源转视椎体方向
        for (int i = 0; i < 4; i++)
        {
            nearCorners[i] = MatTransform(shadowViewInv, nearCorners[i], 1.0f);
            farCorners[i] = MatTransform(shadowViewInv, farCorners[i], 1.0f);
        }
        return points;
    }

    // 用主相机和光源方向更新 CSM 划分
    public void Update(Camera mainCam, Vector3 lightDir)
    {
        mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPlane);
        mainCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), mainCam.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPlane);

        // 视锥体顶点转世界坐标
        for (int i = 0; i < 4; i++)
        {
            farPlane[i] = mainCam.transform.TransformVector(farPlane[i]) + mainCam.transform.position;
            nearPlane[i] = mainCam.transform.TransformVector(nearPlane[i]) + mainCam.transform.position;
        }

        //按照比例划分视椎体
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = farPlane[i] - nearPlane[i];

            near00[i] = nearPlane[i];
            far00[i] = near00[i] + dir * depthDivide[0];

            near01[i] = far00[i];
            far01[i] = near01[i] + dir * depthDivide[1];

            near02[i] = far01[i];
            far02[i] = near02[i] + dir * depthDivide[2];

            near03[i] = far02[i];
            far03[i] = near03[i] + dir * depthDivide[3];
        }

        //形成包围盒
        box0 = LightSpaceAABB(near00, far00, lightDir);
        box1 = LightSpaceAABB(near01, far01, lightDir);
        box2 = LightSpaceAABB(near02, far02, lightDir);
        box3 = LightSpaceAABB(near03, far03, lightDir);

        orthoWidth[0] = Vector3.Magnitude(far00[2] - near00[0]);
        orthoWidth[1] = Vector3.Magnitude(far01[2] - near01[0]);
        orthoWidth[2] = Vector3.Magnitude(far02[2] - near02[0]);
        orthoWidth[3] = Vector3.Magnitude(far03[2] - near03[0]);
    }

    // 将相机配置为第 level 级阴影贴图的绘制模式

    //debug line

    //保存相机参数
    public void SaveCameraSettings(ref Camera camera)
    {        
        settings.position = camera.transform.position;
        settings.rotation = camera.transform.rotation;
        settings.farClipPlane = camera.farClipPlane;
        settings.nearClipPlane = camera.nearClipPlane;
        settings.aspect = camera.aspect;
        camera.orthographic = true;
    }
    //还原相机参数, 更改为透视投影
    public void RevertCameraSettings(ref Camera camera)
    {
        camera.transform.position = settings.position;
        camera.transform.rotation = settings.rotation;
        camera.farClipPlane = settings.farClipPlane;
        camera.nearClipPlane = settings.nearClipPlane;
        camera.aspect = settings.aspect;
        camera.orthographic = false;
    }
    public void ConfigCameraToShadowSpace(ref Camera camera, Vector3 lightDir, int level, float distance, float resolution)
    {
        var box = new Vector3[8];
        var near = new Vector3[4]; var far = new Vector3[4];
        if (level == 0) { box = box0; near = near00; far = far00; };
        if (level == 1) { box = box1; near = near01; far = far01; };
        if (level == 2) { box = box2; near = near02; far = far02; };
        if (level == 3) { box = box3; near = near03; far = far03; };

        //设置中点，宽，高
        Vector3 center = (box[3] + box[4])/2;
        //float width = Vector3.Magnitude(box[0] - box[4]);
        //float height = Vector3.Magnitude(box[0] - box[2]);
        //float len = Mathf.Max(width, height);
        float len = Vector3.Magnitude(far[2] - near[0]);
        float disPerPix = len / resolution;

        Matrix4x4 shadowViewInv = Matrix4x4.LookAt(Vector3.zero, lightDir, Vector3.up);
        Matrix4x4 shadowView = shadowViewInv.inverse;

        //相机坐标旋转到光源坐标下取整
        center = MatTransform(shadowView, center, 1.0f);
        for (int i = 0; i < 3; i++)
            center[i] = Mathf.Floor(center[i] / disPerPix) * disPerPix;
        center = MatTransform(shadowViewInv, center, 1.0f);



        //设置相机
        camera.transform.rotation = Quaternion.LookRotation(lightDir);
        camera.transform.position = center;
        camera.nearClipPlane = -distance;
        camera.farClipPlane = distance;
        camera.aspect = 1.0f;
        camera.orthographicSize = len * 0.5f;
    }
    void DrawFrustum(Vector3[] nearCorners, Vector3[] farCorners, Color color)
    {
        for (int i = 0; i < 4; i++)
            Debug.DrawLine(nearCorners[i], farCorners[i], color);

        Debug.DrawLine(farCorners[0], farCorners[1], color);
        Debug.DrawLine(farCorners[0], farCorners[3], color);
        Debug.DrawLine(farCorners[2], farCorners[1], color);
        Debug.DrawLine(farCorners[2], farCorners[3], color);
        Debug.DrawLine(nearCorners[0], nearCorners[1], color);
        Debug.DrawLine(nearCorners[0], nearCorners[3], color);
        Debug.DrawLine(nearCorners[2], nearCorners[1], color);
        Debug.DrawLine(nearCorners[2], nearCorners[3], color);
    }
    void DrawAABB(Vector3[] points, Color color)
    {
        // 画线
        Debug.DrawLine(points[0], points[1], color);
        Debug.DrawLine(points[0], points[2], color);
        Debug.DrawLine(points[0], points[4], color);

        Debug.DrawLine(points[6], points[2], color);
        Debug.DrawLine(points[6], points[7], color);
        Debug.DrawLine(points[6], points[4], color);

        Debug.DrawLine(points[5], points[1], color);
        Debug.DrawLine(points[5], points[7], color);
        Debug.DrawLine(points[5], points[4], color);

        Debug.DrawLine(points[3], points[1], color);
        Debug.DrawLine(points[3], points[2], color);
        Debug.DrawLine(points[3], points[7], color);
    }
    public void DebugDraw()
    {
        DrawFrustum(nearPlane, farPlane, Color.white);
        DrawAABB(box0, Color.yellow);
        DrawAABB(box1, Color.magenta);
        DrawAABB(box2, Color.green);
        DrawAABB(box3, Color.cyan);
    }
}
