using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamteck.Splines;
using PixelShooter.Pooling;
using UnityEngine;

public static class SplineExtensions
{
    public static SplineFollower FollowTo(
        this GameObject obj,
        ObjectPool pool,
        Transform target,
        float speed,
        Action onComplete = null,
        Action<float> onUpdate = null,
        Spline.Type splineType = Spline.Type.BSpline)
    {
        Vector3 end = target.position;
        Vector3 mid = (obj.transform.position + end) * 0.5f + new Vector3(0f, UnityEngine.Random.Range(4.5f, 7.5f), 0f);
        return FollowToInternal(obj, pool, end, mid, speed, onComplete, onUpdate, splineType);
    }

    public static SplineFollower FollowTo(
        this GameObject obj,
        ObjectPool pool,
        Vector3 targetPos,
        float speed,
        Action onComplete = null,
        Action<float> onUpdate = null,
        Spline.Type splineType = Spline.Type.BSpline)
    {
        Vector3 mid = (obj.transform.position + targetPos) * 0.5f + new Vector3(0f, UnityEngine.Random.Range(4.5f, 7.5f), 0f);
        return FollowToInternal(obj, pool, targetPos, mid, speed, onComplete, onUpdate, splineType);
    }

    public static async UniTask FollowToAsync(
        this GameObject obj,
        ObjectPool pool,
        Vector3 targetPos,
        float speed,
        float arcHeight,
        Action<float> onUpdate,
        CancellationToken cancellationToken,
        Spline.Type splineType = Spline.Type.BSpline)
    {
        if (obj == null)
        {
            return;
        }

        Vector3 start = obj.transform.position;
        float height = Mathf.Max(0f, arcHeight);
        Vector3 mid = (start + targetPos) * 0.5f + new Vector3(0f, height, 0f);

        UniTaskCompletionSource completion = new UniTaskCompletionSource();
        FollowToInternal(
            obj,
            pool,
            targetPos,
            mid,
            speed,
            () => completion.TrySetResult(),
            onUpdate,
            splineType);

        bool released = false;
        CancellationTokenRegistration registration = default;

        void Release(ObjectPool splinePool, bool destroyImmediate)
        {
            if (released || obj == null)
            {
                return;
            }

            released = true;
            ClearExistingFollowers(obj, splinePool, destroyImmediate);
        }

        try
        {
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    Release(pool, false);
                    completion.TrySetCanceled();
                });
            }

            await completion.Task;
        }
        catch (OperationCanceledException)
        {
            Release(pool, false);
            throw;
        }
        finally
        {
            registration.Dispose();
            if (obj != null && !cancellationToken.IsCancellationRequested)
            {
                Release(pool, true);
                obj.transform.position = targetPos;
            }
        }
    }

    private static SplineFollower FollowToInternal(
        GameObject obj,
        ObjectPool pool,
        Vector3 end,
        Vector3 mid,
        float speed,
        Action onComplete,
        Action<float> onUpdate,
        Spline.Type splineType)
    {
        float resolvedSpeed = Mathf.Max(0.01f, speed);
        ClearExistingFollowers(obj, pool);

        if (pool == null)
        {
            onComplete?.Invoke();
            return null;
        }

        GameObject splineObject = pool.SpawnFromPool(PoolTags.TravelSpline, Vector3.zero, Quaternion.identity);
        if (splineObject == null)
        {
            onComplete?.Invoke();
            return null;
        }

        SplineComputer splineComputer = splineObject.GetComponent<SplineComputer>();
        if (splineComputer == null)
        {
            pool.DestroyToPool(PoolTags.TravelSpline, splineObject);
            onComplete?.Invoke();
            return null;
        }

        splineComputer.type = splineType;

        Vector3 start = obj.transform.position;
        SplinePoint[] points = new SplinePoint[3];

        points[0] = new SplinePoint(start);
        points[1] = new SplinePoint(mid);
        points[2] = new SplinePoint(end);

        splineComputer.SetPoints(points);
        splineComputer.RebuildImmediate(true, true);

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
                ReleaseSpline(boundSpline.gameObject, pool);
            else
                ReleaseSpline(splineObject, pool);

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

    public static void ClearExistingFollowers(GameObject obj, ObjectPool pool)
    {
        ClearExistingFollowers(obj, pool, true);
    }

    public static void ClearExistingFollowers(GameObject obj, ObjectPool pool, bool destroyImmediate)
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
                    ReleaseSpline(spline.gameObject, pool);
            }

            DestroyOwned(follower, destroyImmediate);
        }

        ClearShooterFollowerCache(obj);
    }

    private static void ReleaseSpline(GameObject splineObject, ObjectPool pool)
    {
        if (splineObject == null)
            return;

        if (pool != null)
        {
            pool.DestroyToPool(PoolTags.TravelSpline, splineObject);
            return;
        }

        DestroyOwned(splineObject, true);
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
