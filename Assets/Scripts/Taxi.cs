using System.Collections;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Taxi : MonoBehaviour
{
    private Vector3 initialPosition;
    public float moveSpeed = 5.0f;
    private readonly float smooth = 5.0f;
    private Quaternion target;
    public Text text;

    public List<Opponent> opponents;
    public List<DropoffLocation> dropoffLocations;
    private Opponent opponentToBePickedUp;
    private DropoffLocation locationToBeDropedOff;
    private ThirdPerson camera;

    private bool training = false;
    private bool passangerPickedup = false;
    private bool canExecuteAction = true;
    private int actionToBeExecuted = -1;
    private bool done = false;
    /*1 - W
     0 - S
     3 - A
     2 - D
     4 - Pickup
     5 - Dropoff*/

    //Q Learning variables
    float learning_rate = 0.2f;
    float discount_factor = 0.6f;
    float epsilon = 0.2f;
    private float[][] Q = new float[2560][];

    //state
    private int taxiRow = 4;
    private int taxiCol = 1;
    private int passangerRow = 0;
    private int passangerCol = 0;
    private int destinationRow = 0;
    private int destinationCol = 0;
    private int passangerLocationIndex = 0;
    private int destinationLocationIndex = 0;
    private string QFileName = "QMatrix";
    public bool readQFromFileIfExists = false;

    //playing state
    int playingState;

    void OnEnable()
    {
        initialPosition = this.transform.position;
        opponents = new List<Opponent>();
        dropoffLocations = new List<DropoffLocation>();
    }

    void Start()
    {
        camera = GameObject.FindObjectOfType<ThirdPerson>();
        resetTaxiPosition();
        choosePassangerAndDestination();
        initializeQ();
    }

    void Update()
    {
        HandleMovement();
    }

    public void resetTaxiPosition()
    {
        taxiRow = UnityEngine.Random.Range(0, 5);
        taxiCol = UnityEngine.Random.Range(0, 5);

        int taxiX = -45, taxiZ = -45;

        if (taxiRow == 4)
            taxiZ = -45;
        else if (taxiRow == 3)
            taxiZ = -35;
        else if (taxiRow == 2)
            taxiZ = -25;
        else if (taxiRow == 1)
            taxiZ = -15;
        else if (taxiRow == 0)
            taxiZ = -5;

        if (taxiCol == 0)
            taxiX = -45;
        else if (taxiCol == 1)
            taxiX = -35;
        else if (taxiCol == 2)
            taxiX = -25;
        else if (taxiCol == 3)
            taxiX = -15;
        else if (taxiCol == 4)
            taxiX = -5;

        if(!training)
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
            this.transform.position = new Vector3(taxiX, transform.position.y, taxiZ);
        }
    }

    private void initializeQ()
    {
        for (int i = 0; i < 2560; i++)
        {
            Q[i] = new float[6];
            for (int j = 0; j < 6; j++)
            {
                Q[i][j] = 0;
            }
        }
    }

    private List<int> nearWallPositions(int action)
    {
        List<int> positions = new List<int>();
        if (action == 1)
        {
            //edge
            positions.Add(1);
            positions.Add(2);
            positions.Add(3);
            positions.Add(4);
            positions.Add(5);
        }
        else if (action == 2)
        {
            //edge
            positions.Add(5);
            positions.Add(10);
            positions.Add(15);
            positions.Add(20);
            positions.Add(25);

            //wall
            positions.Add(2);
            positions.Add(7);
            positions.Add(16);
            positions.Add(21);
            positions.Add(18);
            positions.Add(23);
            positions.Add(12);
            positions.Add(11);
            positions.Add(13);
        }
        else if (action == 3)
        {
            //edge
            positions.Add(21);
            positions.Add(16);
            positions.Add(11);
            positions.Add(6);
            positions.Add(1);

            //wall
            positions.Add(3);
            positions.Add(8);
            positions.Add(17);
            positions.Add(22);
            positions.Add(19);
            positions.Add(24);
            positions.Add(13);
            positions.Add(14);
            positions.Add(12);
        }
        else if (action == 0)
        {
            //edge
            positions.Add(25);
            positions.Add(24);
            positions.Add(23);
            positions.Add(22);
            positions.Add(21);
        }
        return positions;
    }

    private void resetEnviroment(bool resetWholeEnviroment)
    {
        resetTaxiPosition();

        if (resetWholeEnviroment)
            choosePassangerAndDestination();
        else
            getPassangerAndDestinationCoords();

        done = false;
        passangerPickedup = false;

        if (!training)
        {
            //only for playing
            playingState = encodeState();
        }
    }

    private IEnumerator Q_LearningCooldown()
    {
        Q_Learning();
        yield return new WaitForSeconds(0.5f);
        canExecuteAction = true;
    }

    private void Q_Learning()
    {
        for (int i = 0; i < 100000; i++)
        {
            resetEnviroment(true);

            int state = encodeState();
            int reward = 0;
            int action;

            while (!done)
            {
                try
                {
                    if (randomFloat() < epsilon)
                    {
                        action = UnityEngine.Random.Range(0, 6);
                    }
                    else
                    {
                        action = findBestActionFromState(state);
                    }

                    reward = calculateReward(action);
                    applyAction(action);
                    int nextState = encodeState();

                    float oldValue = Q[state][action];
                    float nextMax = findBestRewardFromState(nextState);

                    float newValue = (1 - learning_rate) * oldValue + learning_rate * (reward + discount_factor * nextMax);
                    Q[state][action] = newValue;

                    state = nextState;
                }
                catch (IndexOutOfRangeException e) { }
                catch (NullReferenceException e) { }
            }

            if(i % 100 == 0)
            {
                //epsilon -= 0.01f;
            }
        }
        text.text = "Training finished!";
        StartCoroutine(clearUIText());
        //writeQtoFile();
        training = false;
        resetEnviroment(true);
    }

    private float randomFloat()
    {
        System.Random rand = new System.Random();
        return (float)rand.NextDouble();
    }

    private int findBestActionFromState(int state)
    {
        float valMaxima = -99999;
        int bestAction = 1;
        for (int action = 0; action < 6; action++)
        {
            try
            {
                if (Q[state][action] > valMaxima)
                {
                    valMaxima = Q[state][action];
                    bestAction = action;
                }
            }
            catch (IndexOutOfRangeException e) { }
            catch (NullReferenceException e) { }

        }
        return bestAction;
    }

    private float findBestRewardFromState(int state)
    {
        float bestReward = -9999;
        for (int action = 0; action < 6; action++)
        {
            try
            {
                if (Q[state][action] > bestReward)
                {
                    bestReward = Q[state][action];
                }
            }
            catch (IndexOutOfRangeException e) { }
            catch (NullReferenceException e) { }
        }
        return bestReward;
    }

    private void applyAction(int action)
    {
        if (action == 0 && !nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            taxiRow++;
        }
        else if (action == 1 && !nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            taxiRow--;
        }
        else if (action == 2 && !nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            taxiCol++;
        }
        else if (action == 3 && !nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            taxiCol--;
        }
        else if (action == 4)
        {
            if (taxiRow == passangerRow && taxiCol == passangerCol)
            {
                passangerPickedup = true;
            }
        }
        else if (action == 5)
        {
            if (taxiRow == destinationRow && taxiCol == destinationCol && passangerPickedup)
            {
                done = true;
            }
        }
    }

    private int calculateReward(int action)
    {
        if (action == 1 && nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            return -10;
        }
        else if (action == 2 && nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            return -10;
        }
        else if (action == 0 && nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            return -10;
        }
        else if (action == 3 && nearWallPositions(action).Contains(encodeTaxiPosition()))
        {
            return -10;
        }
        else if (action == 5)
        {
            if (taxiRow == destinationRow && taxiCol == destinationCol && passangerPickedup)
            {
                return 20;
            }
            else return -10;
        }
        else if (action == 4)
        {
            if (taxiRow != passangerRow || taxiCol != passangerCol)
                return -10;
            else return -1;
        }
        else return -1;
    }

    private int encodeState()
    {
        return encodeTaxiPosition() * 100 + 
            encodePassangerPosition() * 10 + 
            encodeDropoffLocationPosition();
    }

    private int encodeTaxiPosition()
    {
        return (taxiRow * 5 + 1 + taxiCol);
    }

    private int encodePassangerPosition()
    {
        if (passangerPickedup)
        {
            return 5;
        }
        else return passangerLocationIndex;
    }

    private int encodeDropoffLocationPosition()
    {
        return destinationLocationIndex;
    }

    private void HandleMovement()
    {
        if (canExecuteAction)
        {
            if (Input.GetKey(KeyCode.W))
            {
                actionToBeExecuted = 1;
                canExecuteAction = false;
                StartCoroutine(ActionCooldown());
            }
            else if (Input.GetKey(KeyCode.A))
            {
                actionToBeExecuted = 3;
                canExecuteAction = false;
                StartCoroutine(ActionCooldown());
            }
            else if (Input.GetKey(KeyCode.S))
            {
                actionToBeExecuted = 0;
                canExecuteAction = false;
                StartCoroutine(ActionCooldown());
            }
            else if (Input.GetKey(KeyCode.D))
            {
                actionToBeExecuted = 2;
                canExecuteAction = false;
                StartCoroutine(ActionCooldown());
            }
            else if (Input.GetKey(KeyCode.F) && !passangerPickedup)
            {
                if (opponentToBePickedUp.canBePickedUp)
                {
                    opponentToBePickedUp.canBePickedUp = false;
                    opponentToBePickedUp.gameObject.SetActive(false);
                    passangerPickedup = true;
                }
            }
            else if (Input.GetKey(KeyCode.F) && passangerPickedup)
            {
                opponentToBePickedUp.transform.position = this.transform.position;
                opponentToBePickedUp.gameObject.SetActive(true);
                passangerPickedup = false;
            }
            else if (Input.GetKey(KeyCode.L))
            {
                startTraining();
            }
            else if (Input.GetKey(KeyCode.P))
            {
                if (!done)
                {
                    actionToBeExecuted = findBestActionFromState(playingState);
                    applyAction(actionToBeExecuted);
                    playingState = encodeState();
                    canExecuteAction = false;
                    StartCoroutine(ActionCooldown());
                }
            }
            else if (Input.GetKey(KeyCode.R))
            {
                canExecuteAction = false;
                StartCoroutine(resetEnviromentCooldown());
            }
        }
    }

    private void startTraining()
    {
        if (File.Exists(QFileName) && readQFromFileIfExists)
        {
            readQFromFile();
        }
        else
        {
            training = true;
            canExecuteAction = false;
            StartCoroutine(Q_LearningCooldown());
        }
    }

    private void readQFromFile()
    {
        String input = File.ReadAllText(QFileName);

        int i = 0, j = 0;
        foreach (var row in input.Split('\n'))
        {
            j = 0;
            foreach (var col in row.Trim().Split(' '))
            {
                Q[i][j] = float.Parse(col.Trim());
                j++;
            }
            i++;
        }
    }

    private void writeQtoFile()
    {
        using (TextWriter tw = new StreamWriter(QFileName))
        {
            for (int i = 0; i < 2560; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if (j != 0)
                    {
                        tw.Write(" ");
                    }
                    tw.Write(Q[i][j]);
                }
                if(i < 2559)
                    tw.WriteLine();
            }
        }
    }


    private IEnumerator resetEnviromentCooldown()
    {
        resetEnviroment(true);
        yield return new WaitForSeconds(0.1f);
        canExecuteAction = true;
    }
    
    private IEnumerator ActionCooldown()
    {
        yield return new WaitForSeconds(0.5f);
        float transformPositionX = transform.position.x;
        float transformPositionZ = transform.position.z;

        float distanceTraveled = 0;
        while (distanceTraveled < 10.0f)
        {
            if(actionToBeExecuted == 1)
            {
                target = Quaternion.Euler(0, 0, 0);
                transformPositionZ += Time.deltaTime * moveSpeed;
            }
            else if (actionToBeExecuted == 0)
            {
                if (this.transform.position != initialPosition)
                {
                    target = Quaternion.Euler(0, -180, 0);
                    transformPositionZ -= Time.deltaTime * moveSpeed;
                }
            }
            else if (actionToBeExecuted == 3)
            {
                target = Quaternion.Euler(0, -90, 0);
                transformPositionX -= Time.deltaTime * moveSpeed;
            }
            else if (actionToBeExecuted == 2)
            {
                target = Quaternion.Euler(0, 90, 0);
                transformPositionX += Time.deltaTime * moveSpeed;
            }
            else if (actionToBeExecuted == 4)
            {
                if (opponentToBePickedUp.canBePickedUp)
                {
                    opponentToBePickedUp.canBePickedUp = false;
                    opponentToBePickedUp.gameObject.SetActive(false);
                    passangerPickedup = true;
                    text.text = "Passanger has been picked up!";
                    StartCoroutine(clearUIText());
                }
            }
            else if (actionToBeExecuted == 5)
            {
                opponentToBePickedUp.transform.position = this.transform.position +
                    this.transform.TransformDirection(new Vector3(4, 0, 0));
                opponentToBePickedUp.gameObject.SetActive(true);
                opponentToBePickedUp.finalOpponent = false;
                passangerPickedup = false;
                text.text = "Passanger has been dropped off!";
                StartCoroutine(clearUIText());
            }

            transform.position = new Vector3(transformPositionX,
                    transform.position.y,
                    transformPositionZ);

            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * smooth);

            distanceTraveled += Time.deltaTime * moveSpeed;
            yield return null;
        }
        canExecuteAction = true;
    }
    private void choosePassangerAndDestination()
    {
        int passangerIndex = UnityEngine.Random.Range(0, opponents.Count);
        foreach (Opponent opponent in opponents)
        {
            opponent.resetToInitialPosition();
            if (opponents.IndexOf(opponent) != passangerIndex)
            {
                opponent.isActive = false;
                opponent.transform.position = new Vector3(opponent.transform.position.x,
                                                        -10,
                                                        opponent.transform.position.z);
            }
            else
            {
                opponent.isActive = true;
            }
        }
        
        foreach (Opponent opponent in opponents)
        {
            if(opponent.isActive)
            {
                opponentToBePickedUp = opponent;
                break;
            }
        }
        
        int dropoffLocationIndex = UnityEngine.Random.Range(0, dropoffLocations.Count);
        while (dropoffLocations[dropoffLocationIndex].gameObject.name.Equals(opponentToBePickedUp.name))
        {
            dropoffLocationIndex = UnityEngine.Random.Range(0, dropoffLocations.Count);
        }

        foreach (DropoffLocation dropoffLocation in dropoffLocations)
        {
            if (dropoffLocations.IndexOf(dropoffLocation) != dropoffLocationIndex)
            {
                dropoffLocation.isActive = false;
                dropoffLocation.transform.position = new Vector3(dropoffLocation.transform.position.x,
                                                        -10,
                                                        dropoffLocation.transform.position.z);
            }
            else
            {
                dropoffLocation.isActive = true;
                dropoffLocation.resetToInitialPosition();
            }
        }
        
        foreach (DropoffLocation dropoffLocation in dropoffLocations)
        {
            if(dropoffLocation.isActive)
            {
                locationToBeDropedOff = dropoffLocation;
                break;
            }
        }

        getPassangerAndDestinationCoords();
    }

    private void getPassangerAndDestinationCoords()
    {
        if (opponentToBePickedUp.name.Equals("RED"))
        {
            passangerRow = 0;
            passangerCol = 0;
            passangerLocationIndex = 1;
        }
        else if (opponentToBePickedUp.name.Equals("GREEN"))
        {
            passangerRow = 0;
            passangerCol = 4;
            passangerLocationIndex = 2;
        }
        else if (opponentToBePickedUp.name.Equals("YELLOW"))
        {
            passangerRow = 4;
            passangerCol = 0;
            passangerLocationIndex = 3;
        }
        else if (opponentToBePickedUp.name.Equals("BLUE"))
        {
            passangerRow = 4;
            passangerCol = 3;
            passangerLocationIndex = 4;
        }

        if (locationToBeDropedOff.name.Equals("RED"))
        {
            destinationRow = 0;
            destinationCol = 0;
            destinationLocationIndex = 1;
        }
        else if (locationToBeDropedOff.name.Equals("GREEN"))
        {
            destinationRow = 0;
            destinationCol = 4;
            destinationLocationIndex = 2;
        }
        else if (locationToBeDropedOff.name.Equals("YELLOW"))
        {
            destinationRow = 4;
            destinationCol = 0;
            destinationLocationIndex = 3;
        }
        else if (locationToBeDropedOff.name.Equals("BLUE"))
        {
            destinationRow = 4;
            destinationCol = 3;
            destinationLocationIndex = 4;
        }
        StartCoroutine(moveCameraToPassangerAndDestination());
    }

    private IEnumerator moveCameraToPassangerAndDestination()
    {
        camera.setTarget(opponentToBePickedUp.transform);
        yield return new WaitForSeconds(2.0f);
        camera.setTarget(locationToBeDropedOff.transform);
        yield return new WaitForSeconds(2.0f);
        camera.setTarget(this.transform);
    }

    private IEnumerator clearUIText()
    {
        yield return new WaitForSeconds(2.0f);
        text.text = "";
    }
}
