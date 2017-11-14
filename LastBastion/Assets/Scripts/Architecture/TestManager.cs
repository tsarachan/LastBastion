﻿namespace Test {

	using UnityEngine;

	public class TestManager : MonoBehaviour {

		private void Awake(){
			Services.AttackDeck = new AttackerDeck();
			Services.AttackDeck.Setup();
		}


		private void Update(){
			if (Input.GetKeyDown(KeyCode.D)){
				Services.AttackDeck.GetAttackerCard();
			} else if (Input.GetKeyDown(KeyCode.A)){
				Services.AttackDeck.PutCardInDeck(5);
			} else if (Input.GetKeyDown(KeyCode.R)){
				Services.AttackDeck.RemoveCardFromDeck(1);
			}
		}
	}
}