using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Opponent : Fighter
{
    public Transform player;
    public float stopDistance = 1.5f;
    public float attackDistance = 1.45f;
    public float followDistance = 10.0f;
    public bool finalOpponent = false;
    public float finalOpponentDist = 1.0f;
    public TextMeshPro textMesh;
    public bool canBePickedUp = false;
    public bool isActive = true;
    private Vector3 initialPosition = Vector3.zero;
    private Quaternion initialRotation = Quaternion.identity;

    [Range(0f, 1f)]
    public float offensiveRate = 0.2f;
    private bool offensive = false;

    private void OnEnable()
    {
        if (!GameObject.FindObjectOfType<Taxi>().opponents.Contains(this))
        {
            initialPosition = this.transform.position;
            initialRotation = this.transform.rotation;
            GameObject.FindObjectOfType<Taxi>().opponents.Add(this);
        }
    }

    private void OnDestroy()
    {
        Taxi gameObject = GameObject.FindObjectOfType<Taxi>();
        if (gameObject != null)
            gameObject.opponents.Remove(this);
    }

    void Start()
    {
        GetCommonComponents();
        StartCoroutine(SetOffensiveState());
    }

    public void resetToInitialPosition()
    {
        this.transform.position = initialPosition;
        this.transform.rotation = initialRotation;
        this.finalOpponent = true;
    }

    private IEnumerator SetOffensiveState()
    {
        yield return new WaitForSeconds(0.5f);
        offensive = Random.Range(0f, 1f) < offensiveRate;
        yield return StartCoroutine(SetOffensiveState());
    }

    void Update()
    {
        moveDir = (player.position - transform.position);
        moveDir = Vector3.Scale(moveDir, new Vector3(1, 0, 1));

        if (finalOpponent && moveDir.magnitude < finalOpponentDist)
        {
            canBePickedUp = true;
            return;
        }

        if(!finalOpponent)
            animator.SetFloat("distToOpponent", moveDir.magnitude);
        else
            animator.SetFloat("distToOpponent", 100);
        if (moveDir.magnitude < attackDistance && offensive)
            animator.SetTrigger("Punch");

        if (moveDir.magnitude < stopDistance || moveDir.magnitude > followDistance)
            moveDir = Vector3.zero;

        moveDir = moveDir.normalized;
        base.FighterUpdate();
    }
}
