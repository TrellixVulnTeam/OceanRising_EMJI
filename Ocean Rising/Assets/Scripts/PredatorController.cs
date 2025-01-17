﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PredatorController : MonoBehaviour
{
    //Transform player;
    public GameObject player;
    public float sightRange = 30f;
    public float attackRange = 3f;
    public Vector3 randomPoint;
    public float turnTime = 10f;
    [SerializeField] private Animator animator;

    private bool isWandering = true;
    private Vector3 lastPosition;
    private static readonly int Speed = Animator.StringToHash("speed");

    void Awake()
    {
        lastPosition = transform.position;
    }

    void Start()
    {
        //target = PlayerManager.instance.player.transform;
        randomPoint = new Vector3(Random.Range(-30, 30), Random.Range(-30, 30), Random.Range(-30, 30));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }

    void Update()
    {
        if(player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        //var randPosition = randomPoint.transform.position;
        var destination = player.transform.position;
        var step = 2f * Time.deltaTime;
        turnTime -= Time.deltaTime;

        if (Vector3.Distance(transform.position, destination) <= sightRange)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, step);
            transform.LookAt(player.transform);
            //transform.rotation = Quaternion.LookRotation(transform.position, destination - transform.position);
            
            FaceTarget();
            isWandering = false;
        }
        else
        {
            isWandering = true;
        }

        void FaceTarget()
        {
            Vector3 direction = (player.transform.position - transform.position).normalized;
            //Vector3 direction = player.transform.position.normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, direction.y, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        if (isWandering == true)
        {

            RandomTarget();
            if (turnTime <= 0)
            {
                turnTime = 10f;
                randomPoint = new Vector3(transform.position.x + Random.Range(-30, 30), transform.position.y + Random.Range(-30, 30), transform.position.z + Random.Range(-30, 30));
                RandomTarget();
            }

        }

        void RandomTarget()
        {
            transform.position = Vector3.MoveTowards(transform.position, randomPoint, step);
            Vector3 direction = (randomPoint - transform.position).normalized;
            //Vector3 direction = player.transform.position.normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, direction.y, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);

        }
        
        UpdateAnimatorSpeed(); // sets animator speed based on how fast predator is moving

        /*playerInsightRange = Physics.CheckSphere(transform.position, sightRange);
        playerInAttackRange = Physics.CheckSphere(transform.position, attackRange);*/

    }

    void UpdateAnimatorSpeed()
    {
        var position = transform.position;
        var distance = Vector3.Distance(lastPosition, position); // how far has it travelled from last frame
        var speed = distance / Time.deltaTime;
        lastPosition = position; 
        
        animator.SetFloat(Speed, speed);
    }

}
