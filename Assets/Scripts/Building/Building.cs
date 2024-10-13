using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;


namespace Buildings
{

public abstract class Building
{
	public enum Status
	{
		inProgress,
		done
	}

	public Status status;

	public string name;

	public float moneyCost;
	public float buildCost;
	public float buildProgress;

	public abstract void DayAction();

	public Vector3Int location;
	public Vector3Int dims;

	public string inProgressSpritePath;
	public string completeSpritePath;

	public Sprite inProgressSprite;
	public Sprite completeSprite;
	public Sprite currentSprite;

	public List<Tile> tiles;

	//Matrix of coordinates this building occupies row major
	//This should proabbly be one list of tuples but showuld be fine
	//This is fucking inexcusable
	public List<Vector3Int> residentCoordinates;
	public List<Sprite> completeSprites;
	public List<Sprite> currentSprites;
	public List<Sprite> inProgressSprites;

	public void SetResidentCoordinates()
	{
		residentCoordinates = new List<Vector3Int>();
		for (int i=0; i<dims.y; i++)
		{
			for (int j=0; j<dims.x; j++)
			{
				residentCoordinates.Add(new Vector3Int(location.x + j, location.y - i, 0));
			}
		}

	}

	public void GenericStaticInit()
	{
		this.inProgressSprites = new List<Sprite>();
		this.completeSprites = new List<Sprite>();
	}

	public void GenericInit()
	{

		this.tiles = new List<Tile>();

		SetResidentCoordinates();

		IList<Sprite> sList = Resources.LoadAll<Sprite>(completeSpritePath);
		this.completeSprites.AddRange(sList);

		for (int i=0; i<residentCoordinates.Count; i++)
		{

			Sprite s = Resources.Load<Sprite>(inProgressSpritePath);
			this.inProgressSprites.Add(s);
		}

		this.currentSprites = this.inProgressSprites;

		this.status = Status.inProgress;
		this.buildProgress = 0.0f;
	}


	public void AdvanceState(float progress)
	{
		buildProgress += progress;

		if (buildProgress >= buildCost)
		{
			this.status = Status.done;
		}

		if (this.status == Status.done)
			this.currentSprites = this.completeSprites;
	}

}
}
