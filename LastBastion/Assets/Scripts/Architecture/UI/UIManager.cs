﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager {


	/////////////////////////////////////////////
	/// Fields
	/////////////////////////////////////////////


	//UI elements under this script's control
	private Text extraText;
	private Text turnText;
	private CharacterSheetBehavior charSheet;
	private GameObject undoButton;
	private Text momentumText;
	private const string EXTRA_CANVAS = "Extra info canvas";
	private const string TURN_CANVAS = "Turn canvas";
	private const string TEXT_OBJ = "Text";
	private const string CHAR_SHEET_OBJ = "Defender sheet canvas";
	private const string UNDO_BUTTON_OBJ = "Undo button";
	private const string MOMENTUM_OBJ = "Momentum text";


	//text to be written to various UI elements
	private const string TURN = "Turn ";
	private const string BACKSLASH = "/";
	private const string GAME_OVER = "Game over";
	private const string MOMENTUM = "Momentum: ";


	//the attacker's combat cards
	private Transform deckOrganizer;
	private Transform discardOrganizer;
	private List<RectTransform> combatDeck = new List<RectTransform>();
	private const string COMBAT_CARD_OBJ = "Combat card";
	private const string COMBAT_CARD_ORGANIZER = "Draw deck";
	private const string DISCARD_ORGANIZER = "Discard pile";
	private const string ADDED_CARD = " added with value of ";
	private const string VALUE_OBJ = "Value";
	private const float CARD_VERTICAL_SPACE = 0.2f;
	private const float Y_AXIS_MESSINESS = 45.0f;


	/////////////////////////////////////////////
	/// Functions
	/////////////////////////////////////////////


	//initialize variables
	public void Setup(){
		extraText = GameObject.Find(EXTRA_CANVAS).transform.Find(TEXT_OBJ).GetComponent<Text>();
		turnText = GameObject.Find(TURN_CANVAS).transform.Find(TEXT_OBJ).GetComponent<Text>();
		charSheet = GameObject.Find(CHAR_SHEET_OBJ).GetComponent<CharacterSheetBehavior>();
		undoButton = GameObject.Find(UNDO_BUTTON_OBJ);
		ToggleUndoButton();
		momentumText = GameObject.Find(MOMENTUM_OBJ).GetComponent<Text>();
		deckOrganizer = GameObject.Find(COMBAT_CARD_ORGANIZER).transform;
		discardOrganizer = GameObject.Find(DISCARD_ORGANIZER).transform;
		combatDeck.Clear(); //sanity check
		combatDeck = CreateCombatDeck();
	}


	/// <summary>
	/// Special setup function for the test scene; initializes only what the test scene requires
	/// </summary>
	public void TestSetup(){
		deckOrganizer = GameObject.Find(COMBAT_CARD_ORGANIZER).transform;
		discardOrganizer = GameObject.Find(DISCARD_ORGANIZER).transform;
		combatDeck.Clear(); //sanity check
		combatDeck = CreateCombatDeck();
	}

	#region combat deck


	/// <summary>
	/// Create a visible deck of cards for the attackers
	/// </summary>
	private List<RectTransform> CreateCombatDeck(){
		//get rid of the existing cards
		//foreach (Transform card in deckOrganizer) MonoBehaviour.Destroy(card.gameObject);
		foreach (Transform card in discardOrganizer) MonoBehaviour.Destroy(card.gameObject);


		//create a fresh deck
		List<RectTransform> temp = new List<RectTransform>();


		for (int i = 0; i < Services.AttackDeck.GetDeckCount(); i++){
			GameObject newCard = MonoBehaviour.Instantiate<GameObject>(Resources.Load<GameObject>(COMBAT_CARD_OBJ), deckOrganizer);
			newCard.name = COMBAT_CARD_OBJ + " " + i.ToString();
			newCard.transform.localPosition = new Vector3(0.0f, 0.0f, (i * CARD_VERTICAL_SPACE) * -1.0f); //because of the canvas' orientation, must * -1
			temp.Add(newCard.GetComponent<RectTransform>());
		}


		return temp;
	}


	public void RecreateCombatDeck(){
		combatDeck.Clear();
		combatDeck = CreateCombatDeck();
	}


	public void DrawCombatCard(int value){
		Debug.Assert(deckOrganizer.childCount > 0, "No visual card to draw.");

		Transform topCard = deckOrganizer.GetChild(deckOrganizer.childCount - 1);

		topCard.SetParent(discardOrganizer);
		topCard.localPosition = new Vector3(0.0f, 0.0f, ((discardOrganizer.childCount - 1) * CARD_VERTICAL_SPACE) * -1.0f); //-1 b/c don't include this card
		//make the discard a little sloppy, so that it's easier to recognize as distinct from the deck
		topCard.localRotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, Random.Range(-Y_AXIS_MESSINESS, Y_AXIS_MESSINESS)));

		topCard.Find(VALUE_OBJ).GetComponent<Text>().text = value.ToString();
	}


	/// <summary>
	/// Puts a new card on the top of the deck.
	/// 
	/// Note that being on top doesn't mean that card will be drawn first--its value isn't game-relevant. The value is only used
	/// to give the card a descriptive name. Being on top only means that this representation of a card will be the next "drawn" visually.
	/// </summary>
	/// <param name="attacker">The attacker that's adding the card to the deck.</param>
	/// <param name="value">The new card's value.</param>
	public void AddCardToDeck(Transform attacker, int value){
		GameObject newCard = MonoBehaviour.Instantiate<GameObject>(Resources.Load<GameObject>(COMBAT_CARD_OBJ), deckOrganizer);
		newCard.name = COMBAT_CARD_OBJ + ADDED_CARD + value.ToString();
		newCard.transform.localPosition = new Vector3(0.0f, 0.0f, ((deckOrganizer.childCount - 1) * CARD_VERTICAL_SPACE) * -1.0f); //because of the canvas' orientation, must * -1
		combatDeck.Add(newCard.GetComponent<RectTransform>());


		//add a task that puts a card into the visibule deck
		if (!Services.Tasks.CheckForTaskOfType<PutInCardTask>()){ //this is the first/only such task: add the relevant task
			Services.Tasks.AddTask(new PutInCardTask(attacker, deckOrganizer, value));
		} 
		//this is the second or third such task. If it's the second, GetLastTaskOfType() will find the first one and make a new task to follow
		//if it's the third, GetLastTaskOfType() will NOT find the second yet (i.e., will return null).
		//DelayedPutInCardTask will handle waiting until the second is findable, and then add the third task to follow the second
		else {
			if (Services.Tasks.GetLastTaskOfType<PutInCardTask>() == null){
				Services.Tasks.AddTask(new DelayedPutInCardTask(attacker, deckOrganizer, value));
			} else {
				Services.Tasks.GetLastTaskOfType<PutInCardTask>().Then(new PutInCardTask(attacker, deckOrganizer, value));
			}
		}
	}


	/// <summary>
	/// Take a card out of the draw deck. This only affects visuals, not the game logic.
	/// </summary>
	/// <param name="attacker">The attacker that's removing the card from the deck.</param>
	/// <param name="value">The card's value.</param>
	public void RemoveCardFromDeck(Transform attacker, int value){
		//sanity check; if this is somehow trying to remove a card from an empty deck, stop and do nothing
		if (deckOrganizer.childCount == 0) return;

		MonoBehaviour.Destroy(deckOrganizer.GetChild(deckOrganizer.childCount - 1).gameObject);

		//if the card pulled out of the deck was the last card in the deck, reshuffle
		//note that this only affects the visuals; AttackerDeck is responsible for reshuffling the deck within the game's logic
		if (deckOrganizer.childCount == 0){
			combatDeck.Clear();
			combatDeck = CreateCombatDeck();
		}

		if (!Services.Tasks.CheckForTaskOfType<ThrowAwayCardTask>()){
			Services.Tasks.AddTask(new ThrowAwayCardTask(deckOrganizer, attacker, value));
		} else {
			Services.Tasks.GetLastTaskOfType<ThrowAwayCardTask>().Then(new ThrowAwayCardTask(deckOrganizer, attacker, value));
		}
	}


	/// <summary>
	/// Take a card out of the discard pile. This only affects visuals, not the game logic.
	/// </summary>
	/// <param name="attacker">The attacker that's removing the card from the deck.</param>
	/// <param name="value">The card's value.</param>
	public void RemoveCardFromDiscard(Transform attacker, int value){
		//sanity check; if this is somehow trying to remove a card from an empty discard, stop and do nothing
		if (discardOrganizer.childCount == 0) return;

		MonoBehaviour.Destroy(discardOrganizer.GetChild(0).gameObject); //destroy the bottom card in the discard

		foreach (Transform card in discardOrganizer){
			card.localPosition += new Vector3(0.0f, 0.0f, CARD_VERTICAL_SPACE); //add to lower because of the canvas' orientation
		}

		if (!Services.Tasks.CheckForTaskOfType<ThrowAwayCardTask>()){
			Services.Tasks.AddTask(new ThrowAwayCardTask(discardOrganizer, attacker, value));
		} else {
			Services.Tasks.GetLastTaskOfType<ThrowAwayCardTask>().Then(new ThrowAwayCardTask(discardOrganizer, attacker, value));
		}
	}


	#endregion


	/// <summary>
	/// Set the text that displays on the card in the middle-left of the screen.
	/// </summary>
	/// <param name="info">The text to display.</param>
	public void SetExtraText(string info){
		extraText.text = info;
	}


	/// <summary>
	/// Set the text on the card in the middle-right that tracks the current turn.
	/// </summary>
	/// <param name="turn">The current turn.</param>
	/// <param name="totalTurns">How many turns there are in this wave.</param>
	public void SetTurnText(int turn, int totalTurns){
		if (turn <= totalTurns){
			turnText.text = TURN + turn.ToString() + BACKSLASH + totalTurns.ToString();
		}
	}


	/// <summary>
	/// Switch the undo button on or off.
	/// </summary>
	public void ToggleUndoButton(){
		undoButton.SetActive(!undoButton.activeInHierarchy);
	}


	/// <summary>
	/// Write the amount of current momentum.
	/// </summary>
	/// <param name="amount">The current momentum.</param>
	public void SetMomentumText(int amount){
		momentumText.text = MOMENTUM + amount.ToString();
	}


	#region character sheet


	/// <summary>
	/// Change the character sheet to reflect a particular defender, and turn the character sheet on if necessary.
	/// </summary>
	/// <param name="name">The defender's name.</param>
	/// <param name="speed">The defender's speed.</param>
	/// <param name="attackMod">The defender's attack mod.</param>
	/// <param name="armor">The defender's armor.</param>
	/// <param name="defeatsToNextUpgrade">The number of attackers the defender must defeat to upgrade.</param>
	/// <param name="defeatsSoFar">The defender's current progress toward the next upgrade.</param>
	/// <param name="values">The values of the defender's currently available combat cards.</param>
	public void TakeOverCharSheet(string name, int speed, int attackMod, int armor, int defeatsToNextUpgrade, int defeatsSoFar, List<int> values){
		charSheet.RenameSheet(name);
		charSheet.ReviseStatBlock(speed, attackMod, armor);
		charSheet.ReviseNextLabel(defeatsToNextUpgrade - defeatsSoFar);
		charSheet.ReviseAvailCards(values);
		if (!charSheet.gameObject.activeInHierarchy) charSheet.ChangeSheetState();
	}


	/// <summary>
	/// As above, but also updates the upgrade tracks
	/// </summary>
	/// <param name="track1Next">The next upgrade on the left-side track.</param>
	/// <param name="track1Current">The defender's current upgrade on the left-side track.</param>
	/// <param name="track2Next">The next upgrade on the right-side track.</param>
	/// <param name="track2Current">The defender's current upgrade on the right-side track.</param>
	/// <param name="values">The values of the defender's currently available combat cards.</param>
	public void TakeOverCharSheet(string name,
								  int speed,
								  int attackMod,
								  int armor,
								  int defeatsToNextUpgrade,
								  int defeatsSoFar,
								  string track1Next,
								  string track1Current,
								  string track2Next,
								  string track2Current,
								  List<int> values){
		charSheet.ReviseTrack1(track1Next, track1Current);
		charSheet.ReviseTrack2(track2Next, track2Current);
		TakeOverCharSheet(name, speed, attackMod, armor, defeatsToNextUpgrade, defeatsSoFar, values);
	}


	/// <summary>
	/// The character sheet indicates how many attackers the defender must defeat in order to upgrade; this changes
	/// that number.
	/// </summary>
	/// <param name="defeatsToNextUpgrade">The number of attackers the defender must defeat to upgrade.</param>
	/// <param name="defeatsSoFar">The defender's current progress toward the next upgrade.</param>
	public void ReviseNextLabel(int defeatsToNextUpgrade, int defeatsSoFar){
		charSheet.ReviseNextLabel(defeatsToNextUpgrade - defeatsSoFar);
	}


	/// <summary>
	/// Update the character sheet's statement what combat cards are available.
	/// </summary>
	/// <param name="values">A list of the values of the defender's currently available cards.</param>
	public void ReviseCardsAvail(List<int> values){
		charSheet.ReviseAvailCards(values);
	}


	/// <summary>
	/// Switch the character sheet on or off.
	/// </summary>
	public void ChangeCharSheetState(){
		charSheet.ChangeSheetState();
	}


	/// <summary>
	/// Shut off the character sheet.
	/// </summary>
	public void ShutOffCharSheet(){
		charSheet.ShutOffCharSheet();
	}


	/// <summary>
	/// Pick up or put down the character sheet.
	/// </summary>
	public void ShowOrHideSheet(){
		charSheet.DisplayCharSheet();
	}


	/// <summary>
	/// Change the text of the upgrade track on the left.
	/// </summary>
	/// <param name="next">The next upgrade on the left-side track.</param>
	/// <param name="current">The defender's current upgrade on the left-side track.</param>
	public void ReviseTrack1(string next, string current){
		charSheet.ReviseTrack1(next, current);
	}


	/// <summary>
	/// Change the text of the upgrade track on the right.
	/// </summary>
	/// <param name="next">The next upgrade on the right-side track.</param>
	/// <param name="current">The defender's current upgrade on the right-side track.</param>
	public void ReviseTrack2(string next, string current){
		charSheet.ReviseTrack2(next, current);
	}


	/// <summary>
	/// Get whether the character sheet is displayed or hidden.
	/// </summary>
	/// <returns>The character sheet's status.</returns>
	public CharacterSheetBehavior.SheetStatus GetCharSheetStatus(){
		return charSheet.CurrentStatus;
	}

	#endregion
}
