using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using FishNet.Object;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UObject = UnityEngine.Object;

public class UnitCollisionNetworkedPlayModeTests
{
    [UnityTest]
    public IEnumerator TwoNetworkedUnits_MovingToSameDestination_DoNotEndAtSamePosition()
    {
        Camera testCamera = null;
        NetworkManager networkManager = null;
        NetworkObject leftNob = null;
        NetworkObject rightNob = null;
        GameObject leftUnit = null;
        GameObject rightUnit = null;

        try
        {
            testCamera = CreateTestCamera();
            networkManager = CreateRuntimeNetworkManager();
            yield return StartAsHost(networkManager, 5f);

            leftNob = SpawnNetworkedUnit(networkManager, "UnitLeft", new Vector2(-2f, 0f));
            rightNob = SpawnNetworkedUnit(networkManager, "UnitRight", new Vector2(2f, 0f));
            leftUnit = leftNob.gameObject;
            rightUnit = rightNob.gameObject;

            Vector2 sharedDestination = Vector2.zero;
            Vector2 leftStart = leftUnit.transform.position;
            Vector2 rightStart = rightUnit.transform.position;
            RequestMove(leftUnit, sharedDestination);
            RequestMove(rightUnit, sharedDestination);

            for (int i = 0; i < 240; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Vector2 leftPosition = leftUnit.transform.position;
            Vector2 rightPosition = rightUnit.transform.position;
            float distanceBetweenUnits = Vector2.Distance(leftPosition, rightPosition);
            float leftDistanceFromStart = Vector2.Distance(leftStart, leftPosition);
            float rightDistanceFromStart = Vector2.Distance(rightStart, rightPosition);
            float leftDistanceToTarget = Vector2.Distance(leftPosition, sharedDestination);
            float rightDistanceToTarget = Vector2.Distance(rightPosition, sharedDestination);

            Assert.Greater(leftDistanceFromStart, 0.5f, $"Left unit did not move enough. Start={leftStart}, End={leftPosition}");
            Assert.Greater(rightDistanceFromStart, 0.5f, $"Right unit did not move enough. Start={rightStart}, End={rightPosition}");
            Assert.Less(leftDistanceToTarget, Vector2.Distance(leftStart, sharedDestination), "Left unit did not move toward destination.");
            Assert.Less(rightDistanceToTarget, Vector2.Distance(rightStart, sharedDestination), "Right unit did not move toward destination.");
            Assert.Greater(
                distanceBetweenUnits,
                0.05f,
                $"Units ended too close together after moving to a shared destination. Left={leftPosition}, Right={rightPosition}, Distance={distanceBetweenUnits}");
        }
        finally
        {
            if (networkManager != null && networkManager.IsServerStarted)
            {
                if (leftNob != null && leftNob.IsSpawned)
                {
                    networkManager.ServerManager.Despawn(leftNob);
                }

                if (rightNob != null && rightNob.IsSpawned)
                {
                    networkManager.ServerManager.Despawn(rightNob);
                }
            }

            if (leftUnit != null)
            {
                UObject.Destroy(leftUnit);
            }

            if (rightUnit != null)
            {
                UObject.Destroy(rightUnit);
            }

            if (networkManager != null)
            {
                if (networkManager.IsClientStarted)
                {
                    networkManager.ClientManager.StopConnection();
                }

                if (networkManager.IsServerStarted)
                {
                    networkManager.ServerManager.StopConnection(true);
                }

                UObject.Destroy(networkManager.gameObject);
            }

            if (testCamera != null)
            {
                UObject.Destroy(testCamera.gameObject);
            }
        }
    }

    private static NetworkManager CreateRuntimeNetworkManager()
    {
        NetworkManager existing = UObject.FindObjectOfType<NetworkManager>();
        if (existing != null)
        {
            UObject.Destroy(existing.gameObject);
        }

        GameObject managerPrefab = LoadPrefabAtPath("Assets/Prefabs/NetworkManager.prefab");
        Assert.IsNotNull(managerPrefab, "Could not load NetworkManager prefab at Assets/Prefabs/NetworkManager.prefab.");
        GameObject managerGo = UObject.Instantiate(managerPrefab);
        managerGo.name = "TestNetworkManager";
        NetworkManager networkManager = managerGo.GetComponent<NetworkManager>();
        Assert.IsNotNull(networkManager, "NetworkManager prefab is missing NetworkManager component.");
        return networkManager;
    }

    private static IEnumerator StartAsHost(NetworkManager manager, float timeoutSeconds)
    {
        Assert.IsNotNull(manager, "NetworkManager is null.");
        Assert.IsTrue(manager.ServerManager.StartConnection(), "ServerManager.StartConnection returned false.");
        Assert.IsTrue(manager.ClientManager.StartConnection(), "ClientManager.StartConnection returned false.");

        float endTime = Time.realtimeSinceStartup + timeoutSeconds;
        while (!manager.IsHostStarted && Time.realtimeSinceStartup < endTime)
        {
            yield return null;
        }

        Assert.IsTrue(manager.IsHostStarted, $"Host did not start within {timeoutSeconds:0.0}s.");
    }

    private static NetworkObject SpawnNetworkedUnit(NetworkManager manager, string name, Vector2 startPosition)
    {
        GameObject unitPrefab = LoadPrefabAtPath("Assets/Prefabs/Unit.prefab");
        Assert.IsNotNull(unitPrefab, "Could not load Unit prefab at Assets/Prefabs/Unit.prefab.");

        NetworkObject nob = manager.GetPooledInstantiated(unitPrefab, new Vector3(startPosition.x, startPosition.y, 0f), Quaternion.identity, asServer: true);
        Assert.IsNotNull(nob, "FishNet object pool did not return a NetworkObject for Unit prefab.");
        manager.ServerManager.Spawn(nob);
        nob.gameObject.name = name;

        Rigidbody2D rb = nob.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        return nob;
    }

    private static void RequestMove(GameObject unitGo, Vector2 destination)
    {
        Type unitMovementType = FindUnitMovementType();
        Assert.IsNotNull(unitMovementType, "Could not find UnitMovement type in loaded assemblies.");

        Component movement = unitGo.GetComponent(unitMovementType);
        Assert.IsNotNull(movement, "Unit does not have UnitMovement component.");

        MethodInfo requestMove = unitMovementType.GetMethod("RequestMove", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(requestMove, "UnitMovement.RequestMove(Vector2) was not found.");
        requestMove.Invoke(movement, new object[] { destination });
    }

    private static Type FindUnitMovementType()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType("UnitMovement"))
            .FirstOrDefault(t => t != null);
    }

    private static GameObject LoadPrefabAtPath(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
        return null;
#endif
    }

    private static Camera CreateTestCamera()
    {
        Camera existing = Camera.main;
        if (existing != null)
        {
            return existing;
        }

        GameObject cameraGo = new GameObject("TestCamera");
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        return camera;
    }
}
