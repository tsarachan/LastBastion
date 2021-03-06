﻿using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoveBalloonTask : Task {


	/////////////////////////////////////////////
	/// Fields
	/////////////////////////////////////////////


	//the balloon that will move
	private readonly RectTransform balloon;
	private const string BALLOON_OBJ = "Speech balloon";
	private const string CHAT_UI_ORGANIZER = "Chat UI";


	//the text in the balloon
	private readonly string message;
	private readonly TextMeshProUGUI balloonText;
	private const string TEXT_OBJ = "Text";


	//where the balloon is going
	private Vector3 targetLoc;
	private Vector3 direction;
	private const string BALLOON_TARGET = "Speech balloon target";


	//the image for the balloon
	private const string IMAGE_OBJ = "Background";
	private const string PLAYER_BALLOON_IMG = "Sprites/Box Greyscale";
	private const string PLAYER_BALLOON_COLOR_HEX = "#E5FDFFAF";
	private const string OPPONENT_BALLOON_IMG = "Sprites/Grey Box";
	private const string OPPONENT_BALLOON_COLOR_HEX = "#FFE5E5AF";
	private const string OBJECT_BALLOON_IMG = "Sprites/Orange Box";
	private const string OBJECT_BALLOON_COLOR_HEX = "#E6FFE5AF"; //intentionally the same as for opponents


	//speed
	private float speed = 1500.0f;


	//balloon starting location
	private float startDistance;


	//is the balloon growing or shrinking?
	public enum GrowOrShrink { Grow, Shrink };
	private readonly GrowOrShrink change;


	//starting X and Y size
	private float xSize;
	private float ySize;
	private const float CHAT_WINDOW_WIDTH = 150.0f;


	//who or what is speaking
	private readonly ChatUI.BalloonTypes balloonType;


	/////////////////////////////////////////////
	/// Functions
	/////////////////////////////////////////////


	//constructor
	public MoveBalloonTask(Vector3 position, float xSize, float ySize, string message, GrowOrShrink change, ChatUI.BalloonTypes balloonType){
		RectTransform balloon = MonoBehaviour.Instantiate<GameObject>(Resources.Load<GameObject>(BALLOON_OBJ),
																	  GameObject.Find(CHAT_UI_ORGANIZER).transform).GetComponent<RectTransform>();


		this.balloonType = balloonType;
	
		balloon.Find(IMAGE_OBJ).GetComponent<Image>().sprite = AssignBalloonImage(this.balloonType);
		balloon.Find(IMAGE_OBJ).GetComponent<Image>().color = AssignBalloonColor(this.balloonType);

		balloon.transform.position = position;

		this.balloon = balloon;

		this.xSize = xSize;
		this.ySize = ySize;

		this.message = message;

		balloon.transform.Find(TEXT_OBJ).GetComponent<TextMeshProUGUI>().text = "";

		this.change = change;

		//if this is a message that's growing to the size of the chat window, don't let it be bigger than
		//the chat window
		if (change == GrowOrShrink.Grow) this.xSize = CHAT_WINDOW_WIDTH;

		//change the sizeDeltas of the speech balloon and its children, the background image and the text
		balloon.sizeDelta = new Vector2(xSize, ySize);

		foreach (Transform child in balloon.transform){
			child.GetComponent<RectTransform>().sizeDelta = new Vector2(xSize, ySize);
		}
	}


	/// <summary>
	/// Choose the appropriate speech balloon, based on who (or what) is speaking.
	/// 
	/// Right now everyone uses the greyscale box and changes its color. This function provides
	/// functionality for doing it differently in the future.
	/// </summary>
	/// <returns>The balloon image.</returns>
	/// <param name="type">The type of speech balloon.</param>
	private Sprite AssignBalloonImage(ChatUI.BalloonTypes type){
		Sprite temp;

		switch (type){
			case ChatUI.BalloonTypes.Player:
			case ChatUI.BalloonTypes.Opponent:
			case ChatUI.BalloonTypes.Object:
				temp = Resources.Load<Sprite>(PLAYER_BALLOON_IMG);
				break;
			default:
				Debug.Log("Invalid balloon type: " + type.ToString());
				temp = Resources.Load<Sprite>(PLAYER_BALLOON_IMG);
				break;
		}

		return temp;
	}


	private Color AssignBalloonColor(ChatUI.BalloonTypes type){
		Color temp = Color.magenta; //nonsense initialization for error-checking

		switch (type){
		case ChatUI.BalloonTypes.Player:
			ColorUtility.TryParseHtmlString(PLAYER_BALLOON_COLOR_HEX, out temp);
			break;
		case ChatUI.BalloonTypes.Opponent:
			ColorUtility.TryParseHtmlString(OPPONENT_BALLOON_COLOR_HEX, out temp);
			break;
		case ChatUI.BalloonTypes.Object:
			ColorUtility.TryParseHtmlString(OBJECT_BALLOON_COLOR_HEX, out temp);
			break;
		default:
			Debug.Log("Invalid balloon type: " + type.ToString());
			break;
		}

		Debug.Assert(temp != Color.magenta, "Failed to parse color string.");

		return temp;
	}


	/// <summary>
	/// Determine the direction in which the speech balloon is moving, and the total distance to move.
	/// </summary>
	protected override void Init (){
		targetLoc = GameObject.Find(BALLOON_TARGET).transform.position;
		direction = (targetLoc - balloon.transform.position).normalized;
		startDistance = Vector3.Distance(balloon.transform.position, targetLoc);
	}


	/// <summary>
	/// Move the speech balloon each frame, shrinking it as it goes, until it arrives at the chat bar.
	/// </summary>
	public override void Tick (){
		balloon.localScale = ResizeBalloon();

		bool arrived = false;

		balloon.transform.position = MoveBalloon(out arrived);

		if (arrived) SetStatus(TaskStatus.Success);
	}


	/// <summary>
	/// Shrink the balloon based on how close it is to the chat window.
	/// </summary>
	/// <returns>The balloon's scale this frame.</returns>
	private Vector3 ResizeBalloon(){

		if (change == GrowOrShrink.Shrink){
			Vector3 newScale = new Vector3(1.0f, 1.0f, 1.0f);

			float multiplier = Vector3.Distance(balloon.transform.position, targetLoc)/startDistance;

			newScale *= multiplier;

			return newScale;
		} else {
			float newScale = 1.0f - Vector3.Distance(balloon.transform.position, targetLoc)/startDistance;

			return new Vector3(newScale, newScale, newScale);
		}
	}


	/// <summary>
	/// Move the balloon toward the bottom of the chat window.
	/// </summary>
	/// <returns>The balloon's position this frame.</returns>
	/// <param name="done">Has the balloon reached its destination?</param>
	private Vector3 MoveBalloon(out bool done){
		done = false;

		if (Vector3.Distance(balloon.transform.position, targetLoc) <= speed * Time.deltaTime){
			done = true;
			return targetLoc;
		} else {
			return balloon.transform.position + (direction * speed * Time.deltaTime);
		}
	}


	/// <summary>
	/// Get rid of the speech balloon when it reaches the chat window
	/// </summary>
	protected override void OnSuccess (){
		Services.UI.MakeStatement(message, balloonType);
		MonoBehaviour.Destroy(balloon.gameObject);
	}
}
