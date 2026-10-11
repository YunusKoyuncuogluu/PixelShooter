using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using UnityEngine;

public static class SplineExtensions
{

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

        bool released = false;
        CancellationTokenRegistration registration = default;

        void Release(bool destroyImmediate)
        {
            if (released || obj == null)
                return;

            released = true;
            ClearExistingFollowers(obj, destroyImmediate);
        }

        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    Release(false);
                    completion.TrySetCanceled();
                });
            }

            await completion.Task;
        }
        catch (OperationCanceledException)
        {
            Release(false);
            throw;
        }
        finally
        {
            registration.Dispose();
            if (obj != null && !cancellationToken.IsCancellationRequested)
            {
                Release(true);
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

            DestroyOwned(follower, true);
            if (boundSpline != null)
                DestroyOwned(boundSpline.gameObject, true);
            else
                DestroyOwned(splineObject, true);

            if (obj != null)
                obj.transform.position = end;

            ClearShooterFollowerCache(obj);
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
        ClearExistingFollowers(obj, true);
    }

    public static void ClearExistingFollowers(GameObject obj, bool destroyImmediate)
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
                if (spline != null && spline.gameObject != null && spline.gameObject.name.StartsWith("DynamicSpline_"))
                    DestroyOwned(spline.gameObject, destroyImmediate);
            }

            DestroyOwned(follower, destroyImmediate);
        }

        ClearShooterFollowerCache(obj);
    }

    private static void DestroyOwned(UnityEngine.Object target, bool destroyImmediate)
    {
        if (target == null)
            return;

        if (destroyImmediate)
            UnityEngine.Object.DestroyImmediate(target);
        else
            UnityEngine.Object.Destroy(target);
    }

    private static void ClearShooterFollowerCache(GameObject obj)
    {
        ShooterController shooterController = obj.GetComponent<ShooterController>();
        if (shooterController != null)
            shooterController.ClearTravelFollower();
    }
}
