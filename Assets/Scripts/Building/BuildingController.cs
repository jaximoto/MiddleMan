using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

using Utils;
using Buildings;

using static BuildingView;
using static BuildingModel;
using static StatsManager;
using System.Linq;

public class BuildingController : MonoBehaviour
{
	public BuildingModel model;
	public BuildingView view;
	public Tilemap tilemap;
	public StatsManager statsManager;
	public CalendarManager calendarManager;
	public RequestManager requestManager;
	public Building currentBuilding;

	public TMP_InputField workerAllocator;

    // Start is called before the first frame update
    void Start()
    {
		this.model = new BuildingModel();
    }


	void Update()
	{

		EquipBuilding();

		if (Input.GetMouseButtonDown(0)) //TODO Need some way to make it so the fucking player cant just spam click like a fucking monkey
		{
			HandleClick();
		}
		else if (Input.GetMouseButtonDown(1))
		{
			HandleRightClick();
		}

		HighlightTile();

		if (currentBuilding!=null)
			view.RefreshBuildingUI(currentBuilding);
	}


	public void HandleRightClick()
	{
		Vector3Int cell = GetTileCoordinates();

		Debug.Log(!model.doneBuildings.ContainsKey(cell));

		if ((!model.buildings.ContainsKey(cell)) && (!model.doneBuildings.ContainsKey(cell))) return;

		if (model.buildings.ContainsKey(cell))
		{
			Building b = model.buildings[cell];
			model.RemoveBuilding(b, true);
			view.RemoveBuilding(b);
			currentBuilding = null;
			statsManager.ChangeStat(StatType.availableWorkers, b.assignedWorkers);
		}
		else
		{
			Building b = model.doneBuildings[cell];
			model.RemoveBuilding(b, true);
			view.RemoveBuilding(b);
			currentBuilding = null;
			statsManager.ChangeStat(StatType.availableWorkers, b.assignedWorkers);
		}
	}




	public void HighlightTile()
	{
		Vector3Int cell = GetTileCoordinates();
		Tile tile = (Tile)(tilemap.GetTile(cell));
		if (tile==null) return;

		view.HighlightTile(cell, model.lastHighlightedCell, model.lastCellColor);
		model.lastHighlightedCell = cell;
		model.lastCellColor = tile.color;
	}


	public void EquipBuilding()
	{
		//Scroll up
		if (Input.mouseScrollDelta.y > 0)
		{
			model.buildingOptions.Forward();
		} //Scroll down
		else if (Input.mouseScrollDelta.y < 0)
		{
			model.buildingOptions.Backward();
		}

		model.EquipBuilding();

		view.UpdateEquippedBuilding(model.dummyBuilding);
	}


	public void MakeBuilding(Vector3Int pos)
	{
		string currentBuildingName = model.buildingOptions.Current();
		Building b = BuildingFactory.MakeBuilding(currentBuildingName, pos, tilemap);
		if (CheckStats(b))
		{
			model.AddBuilding(b);
			view.UpdateBuilding(b);
			MakeBuildingModifyStats(b);
		}
		else
		{
			view.UpdateNotifyText($"Need ${b.moneyCost} to build a {b.name}.");
		}
	}


	public bool CheckStats(Building b)
	{
		bool enoughMoney = b.moneyCost <= statsManager.GetStatValue(StatType.money);
		return enoughMoney; 
	}


	public void MakeBuildingModifyStats(Building b)
	{
		statsManager.ChangeStat(StatType.money, -b.moneyCost);
		//TODO: Change active workers
	}


	public bool CheckForTiles(List<Vector3Int> coords)
	{
		foreach (Vector3Int coord in coords)
		{
			if (!tilemap.HasTile(coord))
				return false;
		}
		return true;
	}
		

	public void HandleClick()
	{

		Vector3Int clickedCell = GetTileCoordinates();

		// First, am I clicking a building?

		if (model.occupiedTiles.Contains(clickedCell))
		{
			HandleClickBuilding(model.buildings[clickedCell]);
			return;
		}

		// Coordinates this building will occupy
		List<Vector3Int> coords = model.buildingsMap[model.equippedBuildingName].EnumerateCoordinates(clickedCell);

		// Do nothing if building spills outside of tilemap
		if (!CheckForTiles(coords)) return;

		//Check to see if building spills onto an occupied tile 
		if (model.CheckForBuilding(coords))
		{
		}
		else 
		{
			// No building here, call logic to add one 
			MakeBuilding(clickedCell);
		}
		
	}


	public void HandleClickBuilding(Building b)
	{
		currentBuilding = b;
	}


	public void AllocateWorkers()
	{
		if (currentBuilding==null) return;

	  	if (currentBuilding.IsDone()) 
		{
			view.UpdateNotifyText("This building is done");
			return;
		}

		int newWorkers = -1;
		bool validInput = System.Int32.TryParse(workerAllocator.text, out newWorkers);

		if (newWorkers < 0)
		{
			view.UpdateNotifyText("Number must be positive");
			return;
		}

		if (validInput)
		{
			if (newWorkers > statsManager.GetStatValue(StatType.availableWorkers))
			{
				view.UpdateNotifyText("You don't have enough workers");
			}
			else
			{
				int workerDiff = (newWorkers - currentBuilding.assignedWorkers);
				currentBuilding.assignedWorkers = newWorkers;
				statsManager.ChangeStat(StatType.availableWorkers, -workerDiff);
			}
		}
		else
		{
			view.UpdateNotifyText("Just put a number and nothing else in");
		}

		view.ClearWorkerAllocator();
	}


	//TODO: Shit fucking name
	private Vector3Int GetTileCoordinates()
	{
		Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
		Vector3Int clickedCell = tilemap.WorldToCell(mouseWorldPos);
		return clickedCell;
	}	


	private float ComputeProgress(Building b)
	{
		return b.assignedWorkers * statsManager.GetStatValue(StatType.productivity);
	}


	public void AdvanceBuildingStates()
	{
		//TODO
		List<Building> markedForRemoval = new();
		foreach (Building b in model.buildings.Values)
		{
			float progress = ComputeProgress(b);
			b.AdvanceState(progress);
			view.UpdateBuilding(b); 

			if (b.IsDone())
			{
				// call calendar to remove request and get reward
				if (calendarManager.BuildingToDay.ContainsKey(b.name))
				{
					// not perfect
					if (calendarManager.BuildingToDay[b.name].Count != 0)
					{
                        int deadline = calendarManager.BuildingToDay[b.name].Min();

                        if (requestManager.requestDictionary.ContainsKey(deadline))
                        {
                            if (requestManager.requestDictionary[deadline].ContainsKey(b.name))
                            {
                                statsManager.ChangeAptitude(requestManager.requestDictionary[deadline][b.name].relationType, requestManager.requestDictionary[deadline][b.name].GetReward());
                                calendarManager.ClearRequest(b.name, deadline);
                            }
                        }
                    }
					
				}
				markedForRemoval.Add(b);
				
				
				statsManager.ChangeStat(StatType.availableWorkers, b.assignedWorkers);
				b.assignedWorkers = 0;

				foreach (Vector3Int coord in b.residentCoordinates)
				{
					model.doneBuildings.Add(coord, b);
				}
			}
			
		}
        for (int i = 0; i < markedForRemoval.Count; i++)
        {
            model.RemoveBuilding(markedForRemoval[i], false);
        }
    }

}
