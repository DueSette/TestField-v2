﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    #region Data
    public enum GameState { mainMenu, charSelect, characterSpawning, inGame, victoryScreen };
    public GameState theGameState;
    public bool paused = false;

    public Camera cam;
    public Canvas canvas;
    public GameObject portraitsHolder;
    public GameObject selectedPortraits;

    [HideInInspector]
    public GameObject[] selectors = new GameObject[4];      //the pointer game objects that players use to navigate the character selection screen

    [HideInInspector]
    public GameObject[] inGameUIObjects = new GameObject[4];    //The (up to) 4 HUD boxes with the character info (hp, weapon, lives)

    [HideInInspector]
    public GameObject[] inGameChars = new GameObject[4];    //The InGame spawned characters

    [Tooltip("The UI prefab that will be populated with info about the in game status")]
    public GameObject inGameUIObj;

    //Spawning character on game start variables
    public float timeBetweenCharSpawns = 0.2f;
    [Tooltip("Populate with all the desired spawning places. Make sure that they correspond to the transf of an empty object on the map for easier location")]
    public Transform[] spawnLocations = new Transform[4];

    #endregion

    #region Singleton
    public static GameManagerScript gmInstance;
    public void Awake()
    {
        if (gmInstance != null)
        {
            return;
        }
        else
        {
            gmInstance = this;
        }
    }
    #endregion

    private void Update()
    {
        if (GetGameState() == GameState.inGame)
        {
            UpdateInGameUI();
        }
    }

    private void Start()
    {
        SetGameState(GameState.charSelect);

        //populates the selectors array
        for (int i = 0; i < selectors.Length; i++)
        {
            selectors[i] = gameObject.GetComponent<InputManager>().selectors[i];
        }
    }

    //Sets everything for the transition from character selection to gameplay phase
    public IEnumerator StartGameplayLoop()
    {
        if (GetGameState() == GameState.charSelect)
        {
            if (CheckIfReadyToStartGame())
            {
                //shifts the UI away
                StartCoroutine(LerpUI(portraitsHolder, -400));
                StartCoroutine(LerpUI(selectedPortraits, 1200));

                //waits until every char is spawned
                yield return StartCoroutine(SpawnCharacters());
              
                //Should ready the UI to work with each portrait
                InitialiseInGameUI();

                //Waits until the coroutine below returns, which happens only when one character is left
                yield return StartCoroutine(GameOngoing());
            }           
        }
    }

    //Activates and positions the selected characters, also stores them in the InGameChars array
    public IEnumerator SpawnCharacters()
    {
        SetGameState(GameState.characterSpawning);

        int i = 0;

        foreach (GameObject selector in selectors)
        {
            yield return new WaitForSeconds(timeBetweenCharSpawns);
            SelectorBehaviour selScript;
            selScript = selector.GetComponent<SelectorBehaviour>();

            if (selScript.chosenCharacter != null)
            {
                //Stores the chosen characters in the InGameCharacters array
                inGameChars[i] = selScript.chosenCharacter;

                //iterates through the SpawnLocations transforms...
                Transform transform = spawnLocations[i].transform;

                //and assigns its position to the character's transform 
                selScript.chosenCharacter.transform.position = transform.position;

                //finally activates the character
                selScript.chosenCharacter.SetActive(true);
                i++;
            }
        }
        yield return null;
        SetGameState(GameState.inGame);
    }

    //Waits until only one character is left, then calls the endgame functions
    public IEnumerator GameOngoing()
    {
        while (!OneCharacterLeft())
        {
            yield return null;
        }

        SetGameState(GameState.victoryScreen);

        //When only one character is left this part of the code will execute:
        //it should declare the winner, maybe tell the camera to do something cool
        //after a while, call UI buttons to restart/go to character selection/go to main menu
        print("we have a winner");
    }

    //Sets up UI based on the selected characters
    public void InitialiseInGameUI()
    {
        int i = 0;
        foreach (GameObject selector in selectors)
        {
            if (selector.activeSelf)
            {
                //Creates and positions the UI objects, then it communicates to them the character they are portraying
                inGameUIObjects[i] = Instantiate(inGameUIObj, transform.position + new Vector3 (225 + (i * 450), 100, 0), Quaternion.Euler(0, 0, -90));
                inGameUIObjects[i].transform.SetParent(canvas.transform);
                inGameUIObjects[i].GetComponent<InGameUIScript>().representedCharacter = selector.GetComponent<SelectorBehaviour>().chosenCharacter;                
            }
            i++;
        }
    }

    //Updates the UI for the current frame
    public void UpdateInGameUI()
    {
        int i = 0;
        foreach (GameObject uiObj in inGameUIObjects)
        {
            if (uiObj != null)
            {
                inGameUIObjects[i].GetComponent<InGameUIScript>().UpdateHUD();
                i++;
            }
        }
    }

    //Counts how many characters are still alive
    private bool OneCharacterLeft()
    {
        int charactersLeft = 0;
        foreach (GameObject character in inGameChars)
        {
            if(character != null && character.GetComponent<BaseCharacterBehaviour>().GetRemainingLives() > -1)
            {
                charactersLeft++;
            }
        }
        return charactersLeft <= 1;
    }

    //moves the UI pieces from view to outside view or viceversa
    private IEnumerator LerpUI(GameObject theObject, float destination)
    {
        float t = 0;
        Vector3 startPos = theObject.transform.position;
        while (t < 1)
        {
            t += Time.deltaTime;

            theObject.transform.position = Vector3.Lerp(startPos, new Vector3(startPos.x, destination, startPos.z), t * t);
            if (t >= 1)
            {
                theObject.transform.position = new Vector3(startPos.x, destination, startPos.z);
            }
            yield return null;
        }
    }

    private bool CheckIfReadyToStartGame()
    {
        int playersActive = 0;
        int playersReady = 0;

        for (int i = 0; i < selectors.Length; i++)
        {
            if (selectors[i].activeSelf)
            {
                playersActive++;
            }
        }

        for (int i = 0; i < selectors.Length; i++)
        {
            if (selectors[i].GetComponent<SelectorBehaviour>().ready)
            {
                playersReady++;
            }
        }

        //cannot start if no one joined the game/only one person started the game (implement this at the last moment)
        if (playersActive == playersReady && playersActive > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public IEnumerator TogglePause()
    {
        if (!paused)
        {         
            float t = 0.4f;
            float startTime = Time.timeScale;
           
            while (t < 1)
            {
                t += 0.04f;

                Time.timeScale = Mathf.Lerp(startTime, 0, 1 - (t - 1) * (t - 1));

                if (t > 1)
                {
                    Time.timeScale = 0;
                }
                yield return null;
            }       
            yield return null;
        }
        else
        {
            Time.timeScale = 1;
        }
        paused = !paused;
    }

    public GameState GetGameState()
    {
        return theGameState;
    }

    public void SetGameState(GameState gs)
    {
        theGameState = gs;
    }
}
