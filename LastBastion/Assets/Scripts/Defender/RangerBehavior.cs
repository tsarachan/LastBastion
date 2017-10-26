﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RangerBehavior : DefenderSandbox {


	/////////////////////////////////////////////
	/// Fields
	/////////////////////////////////////////////


	//ranger's stats
	private int rangerSpeed = 4;
	private int rangerAttack = 0;
	private int rangerArmor = 1;


	//character sheet
	private const string RANGER_NAME = "Ranger";
	private enum UpgradeTracks { Showboat, Eagle_Eye };


	//the Showboat upgrade track
	private enum ShowboatTrack { None, Showboat, Effortless, Pull_Ahead, Set_the_Standard };
	private List<string> showboatDescriptions = new List<string>() {
		"<b>Start showboating</b>",
		"<size=14><b>Showboat</b></size><size=11>\n\nYou gain extra attacks equal to the number of Horde members you defeated last turn.\n\nYou may attack in any direction.</size>",
		"<size=14><b>Effortless</b></size><size=11>\n\nYou gain extra attacks equal to the number of Horde members you defeated last turn.\n\nYou may attack in any direction.\n\nIf you are behind the target, reduce their attack by 1.</size>",
		"<size=14><b>Pull Ahead</b></size><size=11>\n\nYou gain extra attacks equal to the number of Horde members you defeated last turn.\n\nYou may attack in any direction.\n\nIf you are behind the target, ignore their attack modifier.</size>",
		"<size=14><b>Set the Standard</b></size><size=11>\n\nYou gain extra attacks equal to the number of Horde members you defeated last turn.\n\nYou may attack in any direction.\n\nIf you are behind the target, ignore their attack modifier and armor.</size>",
		"<b>Maximum showboating!</b>"
	};
	private ShowboatTrack currentShowboat;
	private int currentAttacks = 0;
	private int extraAttacks = 0;
	private const int BASE_ATTACKS = 1;

	//UI for the Showboat track
	private Text extraText;
	private const string EXTRA_INFO_OBJ = "Extra info canvas";
	private const string ATTACKS_REMAINING = " attacks left";
	private const string NEXT_ATTACKS = " attacks next turn";
	private const string NEWLINE = "\n";


	/////////////////////////////////////////////
	/// Functions
	/////////////////////////////////////////////


	//do usual setup tasks; also initialize stats with the Ranger's stats, and initialize the upgrade tracks.
	public override void Setup(){
		base.Setup();

		Speed = rangerSpeed;
		AttackMod = rangerAttack;
		Armor = rangerArmor;

		currentShowboat = ShowboatTrack.None;

		extraText = GameObject.Find(EXTRA_INFO_OBJ).transform.Find(TEXT_OBJ).GetComponent<Text>();
	}


	/// <summary>
	/// Make a combat hand for this defender.
	/// </summary>
	/// <returns>A list of cards in the hand.</returns>
	protected override List<Card> MakeCombatHand(){
		return new List<Card>() { new Card(3), new Card(5), new Card(6) };
	}


	/// <summary>
	/// When a Ranger who has moved up the Showboat track gets ready to fight, they gain extra attacks.
	/// </summary>
	public override void PrepareToFight(){
		if (currentShowboat != ShowboatTrack.None){
			currentAttacks = BASE_ATTACKS + extraAttacks;
			extraAttacks = 0; //reset the Ranger's extra attacks; these must be built up again.
		}
	}


	/// <summary>
	/// The Ranger needs to show how many attacks they get when selected to fight while showboating.
	/// </summary>
	public override void BeSelectedForFight(){
		base.BeSelectedForFight();

		if (currentShowboat != ShowboatTrack.None) extraText.text = ReviseAttackText();
	}


	/// <summary>
	/// The Ranger has special combat benefits if moving up the showboat track.
	/// </summary>
	/// <param name="attacker">The attacker this defender is fighting.</param>
	public override void TryFight(AttackerSandbox attacker){
		if (currentShowboat == ShowboatTrack.None){ //don't do anything special if the Ranger isn't showboating
			base.TryFight(attacker);
			return;
		}

		if (!CheckInRange(attacker)) return; //don't fight if the attacker is out of range

		//if the Ranger gets this far, a fight will actually occur; get a combat card for the attacker
		int attackerValue = Services.AttackDeck.GetAttackerCard().Value;


		//the attacker's attack modifier might be reduced if the Ranger is behind them
		if (ChosenCard.Value + AttackMod > attackerValue + DetermineAttackerModifier(attacker)){
			attacker.TakeDamage((ChosenCard.Value + AttackMod) - (attackerValue + DetermineAttackerArmor(attacker) + DetermineAttackerArmor(attacker)));

			//when the Ranger fights, they use up an attack. If they defeat the attacker, they get an extra attack for next turn.
			currentAttacks--;
			extraAttacks++;
			extraText.text = ReviseAttackText();

			FinishWithCard();
			DefeatedSoFar++;
			charSheet.ReviseNextLabel(defeatsToNextUpgrade - DefeatedSoFar);
		} else {
			attacker.FailToDamage();
			FinishWithCard();
			currentAttacks--;
			extraText.text = ReviseAttackText();
		}

		//the Ranger can keep fighting until they run out of attacks
		if (currentAttacks <= 0) DoneFighting();
	}


	/// <summary>
	/// Rangers who are moving up the Showboat track can attack in any direction, including diagonals.
	/// </summary>
	/// <returns><c>true</c> if the attacker is orthogonally or diagonally adjacent, <c>false</c> otherwise.</returns>
	/// <param name="attacker">The attacker being fought.</param>
	private bool CheckInRange(AttackerSandbox attacker){
		if (Mathf.Abs(GridLoc.x - attacker.XPos) <= 1 &&
			Mathf.Abs(GridLoc.z - attacker.ZPos) <= 1) return true;

		return false;
	}


	//the attacker's attack modifier can be altered, or nullified, depending on the Ranger's showboating status and their relative position
	private int DetermineAttackerModifier(AttackerSandbox attacker){
		if (attacker.ZPos >= GridLoc.z) return attacker.AttackMod; //the Ranger has to be behind the attacker (greater Z position) to get a benefit

		switch(currentShowboat){
			case ShowboatTrack.Effortless:
				return attacker.AttackMod - 1;
				break;
			case ShowboatTrack.Pull_Ahead:
			case ShowboatTrack.Set_the_Standard:
				return 0;
				break;
			default:
				return attacker.AttackMod;
				break;
		}
	}


	/// <summary>
	/// If the Ranger is behind the attacker and at maximum showboating, the Ranger nullifies their armor.
	/// </summary>
	/// <returns>The attacker's armor value.</returns>
	/// <param name="attacker">The attacker the Ranger is fighting.</param>
	private int DetermineAttackerArmor(AttackerSandbox attacker){
		if (attacker.ZPos >= GridLoc.z) return attacker.Armor;

		else if (currentShowboat == ShowboatTrack.Set_the_Standard) return 0;
		else return attacker.Armor;
	}


	/// <summary>
	/// Change UI text to provide feedback on how many attacks the Ranger gets.
	/// </summary>
	private string ReviseAttackText(){
		return currentAttacks.ToString() + ATTACKS_REMAINING + NEWLINE +
			   extraAttacks.ToString() + NEXT_ATTACKS;
	}


	/// <summary>
	/// Blank the UI element that provides feedback on the number of attacks remaining.
	/// </summary>
	/// <returns>An empty string.</returns>
	private string BlankAttackText(){
		return "";
	}


	/// <summary>
	/// The Ranger's shutting off cards automatically is more complicated than for a generic defender.
	/// 
	/// Shut off the cards automatically after:
	/// 
	/// 1. The Ranger uses a card, and is not showboating.
	/// 2. The ranger is showboating, and runs out of attacks.
	/// 3. The ranger has used their last card, and the cards have reset.
	/// </summary>
	protected virtual void FinishWithCard(){
		ChosenCard.Available = false;

		FlipCardTask flipTask = new FlipCardTask(uICanvas.GetChild(combatHand.IndexOf(ChosenCard)).GetComponent<RectTransform>(), FlipCardTask.UpOrDown.Down);
		PutDownCardTask putDownTask = new PutDownCardTask(uICanvas.GetChild(combatHand.IndexOf(ChosenCard)).GetComponent<RectTransform>());
		flipTask.Then(putDownTask);

		ChosenCard = null;

		if (!StillAvailableCards()){
			ResetCombatHand();
			ResetHandTask resetTask = new ResetHandTask(this);
			putDownTask.Then(resetTask);

			if (currentShowboat == ShowboatTrack.None || currentAttacks <= 0) resetTask.Then(new ShutOffCardsTask());
		} else if (currentShowboat == ShowboatTrack.None){
			putDownTask.Then(new ShutOffCardsTask());
		} else if (currentAttacks <= 0){
			putDownTask.Then(new ShutOffCardsTask());
		}


		Services.Tasks.AddTask(flipTask);
	}


	/// <summary>
	/// In addition to the normal effects associated with being done fighting, the Ranger needs to blank the sheet
	/// that provides feedback on the number of attacks remaining.
	/// </summary>
	public override void DoneFighting(){
		BeUnselected();

		charSheet.ChangeSheetState();

		extraText.text = BlankAttackText();

		Services.Defenders.DeclareSelfDone(this);
	}


	/// <summary>
	/// Use this defender's name when taking over the character sheet, and display its upgrade paths.
	/// </summary>
	public override void TakeOverCharSheet(){
		charSheet.RenameSheet(RANGER_NAME);
		charSheet.ReviseStatBlock(Speed, AttackMod, Armor);
		charSheet.ReviseTrack1(showboatDescriptions[(int)currentShowboat + 1], showboatDescriptions[(int)currentShowboat]);
		charSheet.ReviseNextLabel(defeatsToNextUpgrade - DefeatedSoFar);
		if (!charSheet.gameObject.activeInHierarchy) charSheet.ChangeSheetState();
	}


	/// <summary>
	/// When the player clicks a button to power up, this function is called.
	/// </summary>
	/// <param>The upgrade tree the player wants to move along.</param>
	/// <param name="tree">The upgrade tree the player clicked. Left is 0, right is 1.</param>
	public override bool PowerUp(int tree){
		if (!base.PowerUp(tree)) return false; //has the Brawler defeated enough attackers to upgrade?

		switch (tree){
			case (int)UpgradeTracks.Showboat:
				if (currentShowboat != ShowboatTrack.Set_the_Standard){
					currentShowboat++;
					charSheet.ReviseTrack1(showboatDescriptions[(int)currentShowboat + 1], showboatDescriptions[(int)currentShowboat]);

					//just started showboating; need to make sure the UI is correct
					if (currentShowboat == ShowboatTrack.Showboat){
						PrepareToFight();
						extraText.text = ReviseAttackText();
					}
				}

				break;
		}

		return true;
	}
}
