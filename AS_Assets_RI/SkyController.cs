using System;
using Unity.VisualScripting;
using UnityEngine;

public class SkyController: MonoBehaviour {



	[Header("Timespan(min) for One Day")]
	// 一日の時間(分)
	public float minPerDay = 5; 
	private int date = 1;

	[Header("Skybox")]
	// 太陽としてのライト


	// 太陽としてのライト
	[Header("Control Sun Pos?")]
	public bool isControlSunPos = false;
	public float sunPosition;

	public GameObject sky;
	private Vector3 skydegree;


	private float degreesPerHour;
	private double initialHour;
	private double gapHour = 0f;
	private bool getStopTime = false;
	private TimeSpan stopTime;
	private double oldTotal;

	private float stopPosition;
	private float deviationPosition;
	private float lastDeviationPosition;

	void InitialDegrees()
	{
			//1 day equals to 5 min, speedup → 24*60/5
			degreesPerHour = 30f * (24 * 60 / minPerDay);
	}

	void InitialTime()
	{
		TimeSpan initialTime = DateTime.Now.TimeOfDay;
	
			initialHour = initialTime.TotalHours;
	}
	void GetGapTime()
    {
		TimeSpan currentTime = DateTime.Now.TimeOfDay;

		if (getStopTime == false)
		{
			stopTime = DateTime.Now.TimeOfDay;//get pause time
			stopPosition=sunPosition;
			getStopTime = true;
		}
		deviationPosition=sunPosition-stopPosition+	lastDeviationPosition;

		gapHour = currentTime.TotalHours - stopTime.TotalHours;//get timespan of current time and pause time
	
	}

	private void Start()
	{
		InitialDegrees();
		InitialTime();
	}

    void Update () {

		if(sunPosition>360.0f||sunPosition<0.0f)
			sunPosition=0.0f;


	
			if (isControlSunPos)
			{	
				GetGapTime();
				skydegree.x=sunPosition;
		  		sky.transform.rotation = Quaternion.Euler(skydegree);
			}
			else
			{
				sunPosition=skydegree.x;
				if (getStopTime)
				{	
					if(Mathf.Abs(deviationPosition)>=0.1f)
						lastDeviationPosition=deviationPosition;
					getStopTime = false;

					initialHour = initialHour+gapHour ;
				}
				UpdateContinuous();
			    sky.transform.rotation = Quaternion.Euler(skydegree);
			}

	
	}



	void UpdateContinuous()
	{
		TimeSpan time = DateTime.Now.TimeOfDay;
	
		//skybox
	
		skydegree.x = ((float)((time.TotalHours- initialHour) * degreesPerHour) / 2+deviationPosition) % 360;


	}



}