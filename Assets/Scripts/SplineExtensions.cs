using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using UnityEngine;

public static class SplineExtensions
{
    /// <summary>
    /// Slot / hedef Transform'a FollowTo. Bitis noktasi target.position (tam slot konumu).
    /// </summary>
    public static SplineFollower FollowTo(
        this GameObject obj,
        Transform target,
        float speed,
        Action onComplete = null,
        Action<float> onUpdate = null,
        Spline.Type splineType = Spline.Type.BSpline)
    {
        Vector3 end = target.position;
        Vector3 mid = (obj.transform.position + end) * 0.5f + new Vector3(0f, UnityEngine.Random.Range(4.5f, 7.5f), 0f);
        return FollowToInternal(obj, end, mid, speed, onComplete, onUpdate, splineType);
    }

    public static SplineFollower FollowTo(
        this GameObject obj,
        Vector3 targetPos,
        float speed,
        Action onComplete = null,
        Action<float> onUpdate = null,
        Spline.Type splineType = Spline.Type.BSpline)
    {
        Vector3 mid = (obj.transform.position + targetPos) * 0.5f + new Vector3(0f, 0.5f, 0f);
        return FollowToInternal(obj, targetPos, mid, speed, onComplete, onUpdate, splineType);
    }

    /// <summary>
    /// Plate seat vb. hedefler icin UniTask'li FollowTo (iptal destekli).
    /// </summary>
    public static async UniTask FollowToAsync(
        this GameObject obj,
        Vector3 targetPos,
        float speed,
        float arcHeight,
        Action<float> onUpdate,
        CancellationToken cancellationToken,
        Spline.Type splineType = Spline.Type.BSpline)
    {
        if (obj == null)
            return;

        Vector3 start = obj.transform.position;
        float height = Mathf.Max(0f, arcHeight);
        Vector3 mid = (start + targetPos) * 0.5f + new Vector3(0f, height, 0f);

        UniTaskCompletionSource completion = new UniTaskCompletionSource();
        FollowToInternal(
            obj,
            targetPos,
            mid,
            speed,
            () => completion.TrySetResult(),
            onUpdate,
            splineType);

        CancellationTokenRegistration registration = default;
        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    ClearExistingFollowers(obj);
                    completion.TrySetCanceled();
                });
            }

            await completion.Task;
        }
        catch (OperationCanceledException)
        {
            ClearExistingFollowers(obj);
            throw;
        }
        finally
        {
            registration.Dispose();
            if (obj != null)
            {
                ClearExistingFollowers(obj);
                obj.transform.position = targetPos;
            }
        }
    }

    private static SplineFollower FollowToInternal(
        GameObject obj,
        Vector3 end,
        Vector3 mid,
        float speed,
        Action onComplete,
        Action<float> onUpdate,
        Spline.Type splineType)
    {
        float resolvedSpeed = Mathf.Max(0.01f, speed);
        ClearExistingFollowers(obj);

        GameObject splineObject = new GameObject("DynamicSpline_" + obj.name);
        SplineComputer splineComputer = splineObject.AddComponent<SplineComputer>();
        splineComputer.type = splineType;

        Vector3 start = obj.transform.position;
        SplinePoint[] points = new SplinePoint[3];
        points[0] = new SplinePoint(start);
        points[1] = new SplinePoint(mid);
        points[2] = new SplinePoint(end);
        splineComputer.SetPoints(points);

        SplineFollower follower = obj.AddComponent<SplineFollower>();
        follower.follow = false;
        follower.spline = splineComputer;
        follower.followMode = SplineFollower.FollowMode.Uniform;
        follower.wrapMode = SplineFollower.Wrap.Default;
        follower.motion.applyRotation = false;
        follower.followSpeed = resolvedSpeed;
        follower.SetPercent(0.0);
        follower.follow = true;

        Action<double> endHandler = null;
        endHandler = _ =>
        {
            follower.onEndReached -= endHandler;
            follower.follow = false;

            SplineComputer boundSpline = follower.spline;
            follower.spline = null;

            if (follower != null)
                UnityEngine.Object.DestroyImmediate(follower);
            if (boundSpline != null)
                UnityEngine.Object.DestroyImmediate(boundSpline.gameObject);
            else if (splineObject != null)
                UnityEngine.Object.DestroyImmediate(splineObject);

            if (obj != null)
                obj.transform.position = end;

            ClearSpoolFollowerCache(obj);
            onComplete?.Invoke();
        };

        follower.onEndReached += endHandler;
        follower.onMotionApplied += () =>
        {
            if (follower == null)
                return;
            onUpdate?.Invoke((float)follower.GetPercent());
        };

        return follower;
    }

    public static void ClearExistingFollowers(GameObject obj)
    {
        if (obj == null)
            return;

        SplineFollower[] existing = obj.GetComponents<SplineFollower>();
        for (int i = 0; i < existing.Length; i++)
        {
            SplineFollower follower = existing[i];
            if (follower == null)
                continue;

            follower.follow = false;
            if (follower.spline != null)
            {
                SplineComputer spline = follower.spline;
                follower.spline = null;
                if (spline != null && spline.gameObject.name.StartsWith("DynamicSpline_"))
                    UnityEngine.Object.DestroyImmediate(spline.gameObject);
            }

            UnityEngine.Object.DestroyImmediate(follower);
        }

        ClearSpoolFollowerCache(obj);
    }

    private static void ClearSpoolFollowerCache(GameObject obj)
    {
        SpoolController spool = obj.GetComponent<SpoolController>();
        if (spool != null)
            spool.InvalidateFollowerCache();
    }
}
