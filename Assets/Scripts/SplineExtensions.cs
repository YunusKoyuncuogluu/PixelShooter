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
        float arcHeight = 3f,
        System.Action onComplete = null)
    {
        // --- Spline Computer ---
        GameObject splineObject = new GameObject("DynamicSpline_" + obj.name);
        SplineComputer splineComputer = splineObject.AddComponent<SplineComputer>();
        splineComputer.type = splineType;

        // --- Spline Points ---
        Vector3 start = obj.transform.position;
        Vector3 end = target.position;
        Vector3 mid = (start + end) / 2f + new Vector3(0, Random.Range(arcHeight * 0.6f, arcHeight), 0);

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

        // --- Bitiş kontrolü ---
        follower.onEndReached += (double f)=>
        {
            onComplete?.Invoke(); // dışarıya haber ver
            Object.Destroy(follower); // follower temizle
            Object.Destroy(splineObject); // spline temizle
        };

        return follower;
    }
}