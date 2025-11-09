using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int playerResources = 500;

    private void Awake()
    {
        // Ensure only one GameManager exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddResources(int amount)
    {
        playerResources += amount;
        Debug.Log("Resources: " + playerResources);
    }

    public bool SpendResources(int amount)
    {
        if (playerResources >= amount)
        {
            playerResources -= amount;
            Debug.Log("Resources: " + playerResources);
            return true;
        }

        Debug.Log("Not enough resources!");
        return false;
    }
}
