using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DropoffLocation : MonoBehaviour
{
    public Transform player;
    public bool isActive = true;
    private Vector3 initialPosition;

    private void OnEnable()
    {
        initialPosition = this.transform.position;
        GameObject.FindObjectOfType<Taxi>().dropoffLocations.Add(this);
    }

    private void OnDestroy()
    {
        Taxi gameObject = GameObject.FindObjectOfType<Taxi>();
        if (gameObject != null)
            gameObject.dropoffLocations.Remove(this);
    }

    public void resetToInitialPosition()
    {
        this.transform.position = initialPosition;
    }
}
