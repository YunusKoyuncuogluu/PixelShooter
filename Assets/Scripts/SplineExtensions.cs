using UnityEngine;
using Dreamteck.Splines;

public static class SplineExtensions
{
    /// <summary>
    /// Objeye spline oluşturur, takip ettirir ve bitince otomatik temizler.
    /// </summary>
    public static SplineFollower FollowTo(
        this GameObject obj,
        Transform target,
        float speed,
        Spline.Type splineType = Spline.Type.BSpline,
        System.Action onComplete = null,
        System.Action<float> onUpdate = null)
    {
        // --- Spline Computer ---
        GameObject splineObject = new GameObject("DynamicSpline_" + obj.name);
        SplineComputer splineComputer = splineObject.AddComponent<SplineComputer>();
        splineComputer.type = splineType;

        // --- Spline Points ---
        Vector3 start = obj.transform.position;
        Vector3 end = new Vector3(target.position.x,target.position.y, 5.68f);
        Vector3 mid = (start + end) / 2f + new Vector3(0, Random.Range(4.5f, 7.5f), 0);

        SplinePoint[] points = new SplinePoint[3];
        points[0] = new SplinePoint(start);
        points[1] = new SplinePoint(mid);
        points[2] = new SplinePoint(end);

        splineComputer.SetPoints(points);

        // --- Spline Follower ---
        SplineFollower follower = obj.AddComponent<SplineFollower>();
        follower.spline = splineComputer;
        follower.followSpeed = speed;
        follower.wrapMode = SplineFollower.Wrap.Default;
        follower.motion.applyRotation = false;

        follower.onMotionApplied += () =>
        {
            float progress = (float)follower.GetPercent();
            onUpdate?.Invoke(progress);
        };

        // --- Bitiş kontrolü ---
        follower.onEndReached += (double f) =>
        {
            onComplete?.Invoke(); // dışarıya haber ver
            Object.Destroy(follower); // follower temizle
            Object.Destroy(splineObject); // spline temizle
        };

        return follower;
    }
    
    public static SplineFollower FollowTo(
        this GameObject obj,
        Vector3 targetPos,
        float speed,
        Spline.Type splineType = Spline.Type.BSpline,
        System.Action onComplete = null,
        System.Action<float> onUpdate = null)
    {
        // --- Spline Computer ---
        GameObject splineObject = new GameObject("DynamicSpline_" + obj.name);
        SplineComputer splineComputer = splineObject.AddComponent<SplineComputer>();
        splineComputer.type = splineType;

        // --- Spline Points ---
        Vector3 start = obj.transform.position;
        Vector3 end = targetPos;
        Vector3 mid = (start + end) / 2f + new Vector3(0, 2f, 0);

        SplinePoint[] points = new SplinePoint[3];
        points[0] = new SplinePoint(start);
        points[1] = new SplinePoint(mid);
        points[2] = new SplinePoint(end);

        splineComputer.SetPoints(points);

        // --- Spline Follower ---
        SplineFollower follower = obj.AddComponent<SplineFollower>();
        follower.spline = splineComputer;
        follower.followSpeed = speed;
        follower.wrapMode = SplineFollower.Wrap.Default;
        follower.motion.applyRotation = false;

        follower.onMotionApplied += () =>
        {
            float progress = (float)follower.GetPercent();
            onUpdate?.Invoke(progress);
        };

        // --- Bitiş kontrolü ---
        follower.onEndReached += (double f) =>
        {
            onComplete?.Invoke(); // dışarıya haber ver
            Object.Destroy(follower); // follower temizle
            Object.Destroy(splineObject); // spline temizle
        };

        return follower;
    }
}