﻿/// <summary>
/// Base class for defenders. All defender "verbs" are contained here.
/// </summary>
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(Rigidbody))]
public class DefenderSandbox : MonoBehaviour {


	/////////////////////////////////////////////
	/// Fields
	/////////////////////////////////////////////


	//defender stats
	public int Speed { get; set; } //in spaces/turn
	public int AttackMod { get; set; }
	public int Armor { get; set; }


	//generic stats for testing purposes
	protected int baseSpeed = 4;
	protected int baseAttackMod = 1;
	protected int baseArmor = 1;


	//is this attacker currently selected? Also includes related variables
	public bool Selected;
	protected GameObject selectedParticle;
	protected const string SELECT_PARTICLE_OBJ = "Selected particle";


	//how many spaces of movement does the defender have left? Also other fields relating to movement
	protected int remainingSpeed = 0;
	protected List<TwoDLoc> moves = new List<TwoDLoc>();
	protected LineRenderer lineRend;
	protected Button moveButton;
	protected const string MOVE_BUTTON_OBJ = "Done moving button";
	protected const string PRIVATE_UI_CANVAS = "Defender canvas";
	[SerializeField] protected float moveSpeed = 1.0f; //movement on screen, as opposed to spaces on the grid
	protected Rigidbody rb;
	protected const float LINE_OFFSET = 0.1f; //picks the movement line up off the board to avoid clipping


	//location in the grid
	protected TwoDLoc GridLoc { get; set; }


	//this defender's hand of cards, along with UI
	protected List<Card> combatHand;
	protected Button noFightButton;
	protected Transform uICanvas;
	protected const string SHARED_UI_CANVAS = "Defender UI canvas";
	protected const string TEXT_OBJ = "Text";
	protected const string NO_FIGHT_BUTTON = "Done fighting button";


	//combat
	public Card ChosenCard { get; set; }
	public const int NO_CARD_SELECTED = 999;


	/////////////////////////////////////////////
	/// Functions
	/////////////////////////////////////////////


	//initialize variables
	public virtual void Setup(){
		Speed = baseSpeed;
		AttackMod = baseAttackMod;
		Armor = baseArmor;
		Selected = false;
		selectedParticle = transform.Find(SELECT_PARTICLE_OBJ).gameObject;
		lineRend = GetComponent<LineRenderer>();
		ClearLine();
		moveButton = transform.Find(PRIVATE_UI_CANVAS).Find(MOVE_BUTTON_OBJ).GetComponent<Button>();
		rb = GetComponent<Rigidbody>();
		GridLoc = new TwoDLoc(0, 0); //default initialization
		combatHand = MakeCombatHand();
		uICanvas = GameObject.Find(SHARED_UI_CANVAS).transform;
		noFightButton = transform.Find(PRIVATE_UI_CANVAS).Find(NO_FIGHT_BUTTON).GetComponent<Button>();
	}


	/// <summary>
	/// Make a combat hand for this defender.
	/// </summary>
	/// <returns>A list of cards in the hand.</returns>
	protected virtual List<Card> MakeCombatHand(){
		return new List<Card>() { new Card(3), new Card(4), new Card(5) };
	}


	/// <summary>
	/// Sets this defender's location in the grid.
	/// </summary>
	/// <param name="x">The grid x coordinate (not world x-position!).</param>
	/// <param name="z">The grid z coordinate (not world z-position!).</param>
	public void NewLoc(int x, int z){
		GridLoc.x = x;
		GridLoc.z = z;
	}


	/// <summary>
	/// Reports whether this defender is in the middle of a move.
	/// </summary>
	/// <returns><c>true</c> if this instance is moving, <c>false</c> otherwise.</returns>
	public virtual bool IsMoving(){
		return remainingSpeed == Speed || remainingSpeed == 0 ? false : true;
	}


	/// <summary>
	/// Carries out all effects associated with being selected to move.
	/// </summary>
	public virtual void BeSelectedForMovement(){
		if (Services.Defenders.IsDone(this)) return; //if this defender has already reported itself done with this phase, it can't be selected

		Selected = true;
		selectedParticle.SetActive(true);
		moveButton.gameObject.SetActive(true);
	}



	/// <summary>
	/// Does everything that needs to happen when another defender is selected.
	/// </summary>
	public virtual void BeUnselected(){
		Selected = false;
		selectedParticle.SetActive(false);
		ChosenCard = null; //relevant for the fight phase
		moveButton.gameObject.SetActive(false);
		noFightButton.gameObject.SetActive(false);
		Services.Defenders.NoSelectedDefender();
		for (int i = 0; i < combatHand.Count; i++) uICanvas.GetChild(i).gameObject.SetActive(false); //shut off the combat cards
	}


	/// <summary>
	/// Call this at the start of the defender movement phase.
	/// </summary>
	public virtual void PrepareToMove(){
		moves.Clear();
		moves.Add(GridLoc);
		remainingSpeed = Speed;
		DrawLine(0, GridLoc.x, GridLoc.z);
	}


	/// <summary>
	/// Whenever the player tries to move a defender, TurnManager calls this function to determine whether the move is legal--
	/// the defender has the movement remaining, the space is legal to enter, etc.
	/// 
	/// A move is illegal if:
	/// 1. It is not adjacent to the defender (or to the last space the defender would move to), or
	/// 2. the space is occupied.
	/// </summary>
	/// <param name="loc">Location.</param>
	public virtual void TryPlanMove(TwoDLoc loc){
		if (moves.Count <= Speed + 1){ //the defender can move up to their speed; they get a + 1 "credit" for the space they're in.
			if (CheckAdjacent(loc, moves[Speed - remainingSpeed]) &&
				Services.Board.GeneralSpaceQuery(loc.x, loc.z) == SpaceBehavior.ContentType.None){
				moves.Add(loc);
				remainingSpeed--;
				DrawLine(Speed - remainingSpeed, loc.x, loc.z);
			}
		}
	}


	protected virtual void DrawLine(int index, int x, int z){
		lineRend.positionCount++;

		Vector3 lineEnd = Services.Board.GetWorldLocation(x, z);
		lineEnd.y += LINE_OFFSET;

		lineRend.SetPosition(index, lineEnd);
	}


	/// <summary>
	/// Called by the UI to move the defender.
	/// </summary>
	public virtual void Move(){
		ClearLine();

		//move on the screen
//		foreach (TwoDLoc move in moves) Debug.Log("moves.x == " + move.x + ", z == " + move.z);


		Services.Tasks.AddTask(new MoveDefenderTask(rb, moveSpeed, moves));


		//move on the grid used for game logic
		Services.Board.TakeThingFromSpace(GridLoc.x, GridLoc.z);
		TwoDLoc destination = moves[moves.Count - 1];
		Services.Board.PutThingInSpace(gameObject, destination.x, destination.z, SpaceBehavior.ContentType.Defender);
		NewLoc(destination.x, destination.z);
		BeUnselected();
		Services.Defenders.DeclareSelfDone(this);

		remainingSpeed = 0;
	}


	/// <summary>
	/// Reset the line players use to plan their movement.
	/// </summary>
	protected virtual void ClearLine(){
		lineRend.positionCount = 0;
	}


	/// <summary>
	/// Determine whether two grid spaces are orthogonally adjacent.
	/// </summary>
	/// <returns><c>true</c>, if so, <c>false</c> if not.</returns>
	/// <param name="next">the grid space being checked.</param>
	/// <param name="current">The space being checked against.</param>
	protected bool CheckAdjacent(TwoDLoc next, TwoDLoc current){
		return ((next.x == current.x && Mathf.Abs(next.z - current.z) == 1) ||
				(Mathf.Abs(next.x - current.x) == 1 && next.z == current.z)) ? true : false;
	}


	/// <summary>
	/// If this defender needs to do anything at the start of the Defender Fight phase, that happens here.
	/// </summary>
	public virtual void PrepareToFight(){
		//generic defenders don't need to do anything
	}


	/// <summary>
	/// Carries out all effects associated with being selected to move.
	/// </summary>
	public virtual void BeSelectedForFight(){
		if (Services.Defenders.IsDone(this)) return; //if this defender has already reported itself done with this phase, it can't be selected

		Selected = true;
		selectedParticle.SetActive(true);
		uICanvas.GetComponent<DefenderUIBehavior>().ClearSelectedColor();

		Debug.Assert(combatHand.Count <= uICanvas.childCount, "Too many combat cards to display!");

		for (int i = 0; i < combatHand.Count; i++){
			uICanvas.GetChild(i).Find(TEXT_OBJ).GetComponent<Text>().text = combatHand[i].Value.ToString();

			uICanvas.GetChild(i).GetComponent<Image>().color = AssignCardColor(i);

			uICanvas.GetChild(i).gameObject.SetActive(true);
		}

		ChosenCard = null;

		noFightButton.gameObject.SetActive(true);
	}


	/// <summary>
	/// Determines the appropriate color for a card based on its state.
	/// </summary>
	/// <returns>The card's color.</returns>
	/// <param name="index">The index of the card, in the defender's combat hand AND the shared UI canvas' children.</param>
	protected virtual Color AssignCardColor(int index){
		if (combatHand[index].Available) return Color.white;
		else return Color.red;
	}


	/// <summary>
	/// Note which combat card the player has chosen. Reject attempts to choose a card that isn't available.
	/// </summary>
	/// <param name="index">The card's number, zero-indexed.</param>
	public virtual void AssignChosenCard(int index){
		if (combatHand[index].Available){
			ChosenCard = combatHand[index];
			uICanvas.GetChild(index).GetComponent<Image>().color = Color.blue;
		}
	}


	/// <summary>
	/// Returns the chosen card's value.
	/// </summary>
	/// <returns>The value; if no card is currently selected, this will be 999 (NO_CARD_SELECTED).</returns>
	public virtual int GetChosenCardValue(){
		if (ChosenCard == null) return NO_CARD_SELECTED;
		return ChosenCard.Value;
	}


	/// <summary>
	/// Damages an attacker if it is directly north and the player has chosen a stronger card than its value.
	/// </summary>
	/// <param name="attacker">The attacker this defender is fighting.</param>
	public virtual void TryFight(AttackerSandbox attacker){
		if (!CheckIsNorth(attacker)) return; //don't fight if the attacker isn't directly to the north

		//if the Defender gets this far, a fight will actually occur; get a combat card for the attacker
		int attackerValue = Services.AttackDeck.GetAttackerCard().Value;

		if (ChosenCard.Value + AttackMod > attackerValue + attacker.Armor){
			attacker.TakeDamage((ChosenCard.Value + AttackMod) - (attackerValue + attacker.Armor));
			FinishWithCard();
			DoneFighting();
		} else {
			attacker.FailToDamage();
			FinishWithCard();
			DoneFighting();
		}
	}


	/// <summary>
	/// Is an attacker directly north of this defender?
	/// </summary>
	/// <returns><c>true</c> if the attacker is one space north, <c>false</c> otherwise.</returns>
	/// <param name="attacker">The attacker being checked.</param>
	protected bool CheckIsNorth(AttackerSandbox attacker){
		if (attacker.XPos == GridLoc.x && attacker.ZPos == GridLoc.z + 1) return true;
		return false;
	}


	/// <summary>
	/// Handle everything that needs to happen to the player's chosen card when a defender has finished a combat.
	/// </summary>
	protected virtual void FinishWithCard(){
		ChosenCard.Available = false;
		uICanvas.GetChild(combatHand.IndexOf(ChosenCard)).GetComponent<Image>().color = AssignCardColor(combatHand.IndexOf(ChosenCard));
		ChosenCard = null;

		if (!StillAvailableCards()) ResetCombatHand();
	}


	/// <summary>
	/// Are there any available cards in this defender's combat hand?
	/// </summary>
	/// <returns><c>true</c> if so, <c>false</c> if not.</returns>
	protected bool StillAvailableCards(){
		bool temp = false;

		foreach (Card card in combatHand){
			if (card.Available){
				temp = true;
				break;
			}
		}

		return temp;
	}


	/// <summary>
	/// Resets a defender's combat hand, making the cards available for use and providing relevant feedback.
	/// </summary>
	protected void ResetCombatHand(){
		foreach (Card card in combatHand){
			card.Available = true;
			uICanvas.GetChild(combatHand.IndexOf(card)).GetComponent<Image>().color = AssignCardColor(combatHand.IndexOf(card));
		}
	}


	/// <summary>
	/// When this defender is done fighting, this carries out all associated effects.
	/// </summary>
	public virtual void DoneFighting(){
		BeUnselected();

		Services.Defenders.DeclareSelfDone(this);
	}
}
