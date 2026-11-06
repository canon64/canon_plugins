// Generated from the installed KKS row-layout evidence by analysis/kks-ai-conversation/generate_layout.py.
// Only table grouping is retained; no scene, game manager, random selection, or result mutation.
using System.Collections.Generic;
namespace MainGameAiShoujoMode.Conversation
{
internal sealed partial class KksConversationLayout
{
	private void Parse(List<Row> _rows)
	{
		dicTalkInfo = new Dictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>>();
		int row = N("native.layout.0");
		row = LoadGenericOnly(_rows, row, N("native.layout.0"), Group.Introduction, N("native.layout.14"));
		List<BasicInfo> listCommand = GetListCommand(N("native.layout.0"), Group.Talk, N("native.layout.0"));
		int failureRow = row + N("native.layout.6");
		for (int i = N("native.layout.0"); i < N("native.layout.2"); i++)
		{
			listCommand.Add(CreateBranch(_rows, ref row, N("native.layout.2"), failureRow));
		}
		row++;
		for (int j = N("native.layout.0"); j < N("native.layout.3"); j++)
		{
			listCommand.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		}
		List<BasicInfo> listCommand2 = GetListCommand(N("native.layout.0"), Group.Talk, N("native.layout.1"));
		int failureRow2 = row + N("native.layout.9");
		for (int k = N("native.layout.0"); k < N("native.layout.3"); k++)
		{
			listCommand2.Add(CreateBranch(_rows, ref row, N("native.layout.2"), failureRow2));
		}
		row++;
		for (int l = N("native.layout.0"); l < N("native.layout.3"); l++)
		{
			listCommand2.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		}
		for (int m = N("native.layout.0"); m < N("native.layout.6"); m++)
		{
			listCommand2.Add(CreateBranch(_rows, ref row, N("native.layout.1"), row + N("native.layout.2")));
			row++;
		}
		List<BasicInfo> listCommand3 = GetListCommand(N("native.layout.0"), Group.Talk, N("native.layout.2"));
		int failureRow3 = row + N("native.layout.9");
		for (int n = N("native.layout.0"); n < N("native.layout.3"); n++)
		{
			listCommand3.Add(CreateBranch(_rows, ref row, N("native.layout.2"), failureRow3));
		}
		row++;
		for (int num = N("native.layout.0"); num < N("native.layout.3"); num++)
		{
			listCommand3.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		}
		LoadTouch(N("native.layout.0"), Group.See, N("native.layout.2"), _rows, ref row);
		LoadTouch(N("native.layout.0"), Group.Touch, N("native.layout.4"), _rows, ref row);
		row = LoadGenericOnly(_rows, row, N("native.layout.0"), Group.Interlude, N("native.layout.2"));
		row = LoadGenericOnly(_rows, row, N("native.layout.0"), Group.LeaveAlone, N("native.layout.2"));
		row = LoadGenericOnly(_rows, row, N("native.layout.0"), Group.EndConversation, N("native.layout.9"));
		row = LoadGenericOnly(_rows, row, N("native.layout.0"), Group.EndConversation, N("native.layout.1"));
		LoadEvent(N("native.layout.0"), N("native.layout.0"), N("native.layout.1"), N("native.layout.0"), _rows, ref row);
		LoadEvent(N("native.layout.0"), N("native.layout.1"), N("native.layout.1"), N("native.layout.0"), _rows, ref row);
		LoadEvent(N("native.layout.0"), N("native.layout.2"), N("native.layout.1"), N("native.layout.0"), _rows, ref row);
		LoadEvent(N("native.layout.0"), N("native.layout.3"), N("native.layout.1"), N("native.layout.0"), _rows, ref row);
		LoadEvent(N("native.layout.0"), N("native.layout.4"), N("native.layout.1"), N("native.layout.0"), _rows, ref row);
		row = LoadGenericOnly(_rows, row, N("native.layout.1"), Group.Introduction, N("native.layout.17"));
		List<BasicInfo> listCommand4 = GetListCommand(N("native.layout.1"), Group.Talk, N("native.layout.0"));
		int noInterestedRow = row + N("native.layout.12");
		for (int num2 = N("native.layout.0"); num2 < N("native.layout.4"); num2++)
		{
			listCommand4.Add(CreateTalk(_rows, ref row, noInterestedRow));
		}
		row++;
		listCommand4.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		List<BasicInfo> listCommand5 = GetListCommand(N("native.layout.1"), Group.Talk, N("native.layout.1"));
		int noInterestedRow2 = row + N("native.layout.12");
		for (int num3 = N("native.layout.0"); num3 < N("native.layout.4"); num3++)
		{
			listCommand5.Add(CreateTalk(_rows, ref row, noInterestedRow2));
		}
		row++;
		listCommand5.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		List<BasicInfo> listCommand6 = GetListCommand(N("native.layout.1"), Group.Talk, N("native.layout.2"));
		int noInterestedRow3 = row + N("native.layout.12");
		for (int num4 = N("native.layout.0"); num4 < N("native.layout.4"); num4++)
		{
			listCommand6.Add(CreateTalk(_rows, ref row, noInterestedRow3));
		}
		row++;
		listCommand6.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		LoadTouch(N("native.layout.1"), Group.See, N("native.layout.2"), _rows, ref row);
		LoadTouch(N("native.layout.1"), Group.Touch, N("native.layout.4"), _rows, ref row);
		row = LoadGenericOnly(_rows, row, N("native.layout.1"), Group.Interlude, N("native.layout.3"));
		row = LoadGenericOnly(_rows, row, N("native.layout.1"), Group.LeaveAlone, N("native.layout.2"));
		row = LoadGenericOnly(_rows, row, N("native.layout.1"), Group.EndConversation, N("native.layout.9"));
		LoadEvent(N("native.layout.1"), N("native.layout.6"), N("native.layout.2"), N("native.layout.1"), _rows, ref row);
		LoadEvent(N("native.layout.1"), N("native.layout.7"), N("native.layout.4"), N("native.layout.1"), _rows, ref row);
		LoadEvent(N("native.layout.1"), N("native.layout.0"), N("native.layout.1"), N("native.layout.1"), _rows, ref row);
		LoadEvent(N("native.layout.1"), N("native.layout.1"), N("native.layout.1"), N("native.layout.1"), _rows, ref row);
		LoadEvent(N("native.layout.1"), N("native.layout.2"), N("native.layout.1"), N("native.layout.1"), _rows, ref row);
		LoadEvent(N("native.layout.1"), N("native.layout.3"), N("native.layout.1"), N("native.layout.1"), _rows, ref row);
		LoadEvent(N("native.layout.1"), N("native.layout.4"), N("native.layout.1"), N("native.layout.0"), _rows, ref row);
		row = LoadGenericOnly(_rows, row, N("native.layout.2"), Group.Introduction, N("native.layout.17"));
		List<BasicInfo> listCommand7 = GetListCommand(N("native.layout.2"), Group.Talk, N("native.layout.0"));
		int noInterestedRow4 = row + N("native.layout.12");
		for (int num5 = N("native.layout.0"); num5 < N("native.layout.4"); num5++)
		{
			listCommand7.Add(CreateTalk(_rows, ref row, noInterestedRow4));
		}
		row++;
		for (int num6 = N("native.layout.0"); num6 < N("native.layout.2"); num6++)
		{
			listCommand7.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		}
		List<BasicInfo> listCommand8 = GetListCommand(N("native.layout.2"), Group.Talk, N("native.layout.1"));
		int noInterestedRow5 = row + N("native.layout.12");
		for (int num7 = N("native.layout.0"); num7 < N("native.layout.4"); num7++)
		{
			listCommand8.Add(CreateTalk(_rows, ref row, noInterestedRow5));
		}
		row++;
		for (int num8 = N("native.layout.0"); num8 < N("native.layout.2"); num8++)
		{
			listCommand8.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		}
		List<BasicInfo> listCommand9 = GetListCommand(N("native.layout.2"), Group.Talk, N("native.layout.2"));
		int noInterestedRow6 = row + N("native.layout.12");
		for (int num9 = N("native.layout.0"); num9 < N("native.layout.4"); num9++)
		{
			listCommand9.Add(CreateTalk(_rows, ref row, noInterestedRow6));
		}
		row++;
		for (int num10 = N("native.layout.0"); num10 < N("native.layout.2"); num10++)
		{
			listCommand9.Add(CreateGeneric(_rows, ref row, N("native.layout.2")));
		}
		LoadTouch(N("native.layout.2"), Group.See, N("native.layout.2"), _rows, ref row);
		LoadTouch(N("native.layout.2"), Group.Touch, N("native.layout.4"), _rows, ref row);
		row = LoadGenericOnly(_rows, row, N("native.layout.2"), Group.Interlude, N("native.layout.3"));
		row = LoadGenericOnly(_rows, row, N("native.layout.2"), Group.LeaveAlone, N("native.layout.2"));
		row = LoadGenericOnly(_rows, row, N("native.layout.2"), Group.EndConversation, N("native.layout.9"));
		int num11 = N("native.layout.-1");
		int num12 = N("native.layout.-1");
		List<BasicInfo> listCommand10 = GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.7"));
		listCommand10.Add(CreateGeneric(_rows, row++));
		num11 = row;
		listCommand10.Add(CreateTalk(_rows, ref row, N("native.layout.-1"), _introduction: false));
		num12 = row;
		listCommand10.Add(CreateSelect(_rows, ref row, N("native.layout.2")));
		for (int num13 = N("native.layout.0"); num13 < N("native.layout.2"); num13++)
		{
			listCommand10.Add(CreateBranch(_rows, ref row, N("native.layout.1"), row + N("native.layout.2")));
			row++;
		}
		List<BasicInfo> listCommand11 = GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.0"));
		listCommand11.Add(CreateGeneric(_rows, row++));
		listCommand11.Add(CreateSelect(_rows, ref row, N("native.layout.2")));
		List<BasicInfo> listCommand12 = GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.1"));
		listCommand12.Add(CreateGeneric(_rows, row++));
		listCommand12.Add(CreateSelect(_rows, ref row, N("native.layout.2")));
		List<BasicInfo> listCommand13 = GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.2"));
		listCommand13.Add(CreateGeneric(_rows, row++));
		listCommand13.Add(CreateSelect(_rows, ref row, N("native.layout.2")));
		List<BasicInfo> listCommand14 = GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.3"));
		listCommand14.Add(CreateGeneric(_rows, row++));
		listCommand14.Add(CreateSelect(_rows, ref row, N("native.layout.2")));
		GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.8")).Add(CreateBranch(_rows, ref row, N("native.layout.1"), row + N("native.layout.2")));
		row++;
		GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.4")).Add(CreateBranch(_rows, ref row, N("native.layout.1"), row + N("native.layout.2")));
		row++;
		row += N("native.layout.10");
		int num14 = row;
		LoadGenericOnly(_rows, num14 + N("native.layout.12"), N("native.layout.0"), Group.Introduction, N("native.layout.1"));
		LoadGenericOnly(_rows, num14 + N("native.layout.10"), N("native.layout.1"), Group.Introduction, N("native.layout.1"));
		LoadGenericOnly(_rows, num14 + N("native.layout.13"), N("native.layout.1"), Group.Introduction, N("native.layout.2"));
		LoadGenericOnly(_rows, num14 + N("native.layout.11"), N("native.layout.2"), Group.Introduction, N("native.layout.1"));
		LoadGenericOnly(_rows, num14 + N("native.layout.15"), N("native.layout.2"), Group.Introduction, N("native.layout.2"));
		List<BasicInfo> listCommand15 = GetListCommand(N("native.layout.2"), Group.Talk, N("native.layout.0"));
		listCommand15.Add(CreateGeneric(_rows, ref row, N("native.layout.1")));
		listCommand15.Add(CreateTalk(_rows, ref row));
		List<BasicInfo> listCommand16 = GetListCommand(N("native.layout.2"), Group.Interlude, N("native.layout.0"));
		listCommand16.Add(CreateGeneric(_rows, ref row, N("native.layout.1")));
		listCommand16.Add(CreateGeneric(_rows, ref row, N("native.layout.3")));
		List<BasicInfo> listCommand17 = GetListCommand(N("native.layout.2"), Group.Event, N("native.layout.7"));
		BranchInfo branchInfo = CreateTalk(_rows, ref num11, N("native.layout.-1"), _introduction: false);
		branchInfo.success[N("native.layout.0")] = LoadData(_rows[num14 + N("native.layout.8")], ref branchInfo.priority, ref branchInfo.conditions);
		listCommand17.Add(branchInfo);
		SelectInfo selectInfo = CreateSelect(_rows, ref num12, N("native.layout.2"));
		selectInfo.introduction = LoadData(_rows[num14 + N("native.layout.9")], ref selectInfo.priority, ref selectInfo.conditions);
		listCommand17.Add(selectInfo);
	}

	private Dictionary<int, List<BasicInfo>> GetDictionaryGroup(int _stage, Group _group)
	{
		if (!dicTalkInfo.ContainsKey(_stage))
		{
			dicTalkInfo.Add(_stage, new Dictionary<Group, Dictionary<int, List<BasicInfo>>>());
		}
		Dictionary<Group, Dictionary<int, List<BasicInfo>>> dictionary = dicTalkInfo[_stage];
		if (!dictionary.ContainsKey(_group))
		{
			dictionary.Add(_group, new Dictionary<int, List<BasicInfo>>());
		}
		return dictionary[_group];
	}

	private List<BasicInfo> GetListCommand(int _stage, Group _group, int _command)
	{
		Dictionary<int, List<BasicInfo>> dictionaryGroup = GetDictionaryGroup(_stage, _group);
		if (!dictionaryGroup.ContainsKey(_command))
		{
			dictionaryGroup.Add(_command, new List<BasicInfo>());
		}
		return dictionaryGroup[_command];
	}

	private BranchInfo CreateBranch(List<Row> _list, ref int _startRow, int _successNum, int _failureRow)
	{
		BranchInfo branchInfo = new BranchInfo(_startRow)
		{
			success = new Row[_successNum]
		};
		branchInfo.introduction = LoadData(_list[_startRow++], ref branchInfo.priority, ref branchInfo.conditions);
		for (int i = N("native.layout.0"); i < _successNum; i++)
		{
			branchInfo.success[i] = LoadData(_list[_startRow++]);
		}
		branchInfo.failure = LoadData(_list[_failureRow]);
		return branchInfo;
	}

	private BranchInfo CreateTalk(List<Row> _list, ref int _startRow, int _noInterestedRow, bool _introduction = true)
	{
		BranchInfo branchInfo = new BranchInfo(_startRow)
		{
			success = new Row[N("native.layout.1")]
		};
		if (_introduction)
		{
			branchInfo.introduction = LoadData(_list[_startRow++], ref branchInfo.priority, ref branchInfo.conditions);
			branchInfo.success[N("native.layout.0")] = LoadData(_list[_startRow++]);
		}
		else
		{
			branchInfo.introduction = null;
			branchInfo.success[N("native.layout.0")] = LoadData(_list[_startRow++], ref branchInfo.priority, ref branchInfo.conditions);
		}
		branchInfo.failure = LoadData(_list[_startRow++]);
		if (_noInterestedRow != N("native.layout.-1"))
		{
			branchInfo.noInterested = LoadData(_list[_noInterestedRow]);
		}
		return branchInfo;
	}

	private int LoadGenericOnly(List<Row> _list, int _row, int _stage, Group _group, int _num)
	{
		List<BasicInfo> listCommand = GetListCommand(_stage, _group, N("native.layout.0"));
		int _priority = N("native.layout.0");
		int _conditions = N("native.layout.-1");
		for (int i = N("native.layout.0"); i < _num; i++)
		{
			listCommand.Add(new GenericInfo(_row, LoadData(_list[_row++], ref _priority, ref _conditions))
			{
				priority = _priority,
				conditions = _conditions
			});
		}
		return _row;
	}

	private GenericInfo CreateGeneric(List<Row> _list, ref int _startRow, int _num)
	{
		GenericInfo genericInfo = new GenericInfo(_startRow, _num);
		for (int i = N("native.layout.0"); i < _num; i++)
		{
			genericInfo.talk[i] = ((i == N("native.layout.0")) ? LoadData(_list[_startRow++], ref genericInfo.priority, ref genericInfo.conditions) : LoadData(_list[_startRow++]));
		}
		return genericInfo;
	}

	private GenericInfo CreateGeneric(List<Row> _list, int _row)
	{
		GenericInfo genericInfo = new GenericInfo(_row, N("native.layout.1"));
		genericInfo.talk[N("native.layout.0")] = LoadData(_list[_row], ref genericInfo.priority, ref genericInfo.conditions);
		return genericInfo;
	}

	private SelectInfo CreateSelect(List<Row> _list, ref int _startRow, int _num, int _failureRow)
	{
		SelectInfo selectInfo = new SelectInfo(_startRow, _num);
		selectInfo.introduction = LoadData(_list[_startRow++], ref selectInfo.priority, ref selectInfo.conditions);
		for (int i = N("native.layout.0"); i < _num; i++)
		{
			selectInfo.choice[i] = LoadChoiceData(_list[_startRow++]);
		}
		if (_failureRow != N("native.layout.-1"))
		{
			selectInfo.failure = LoadData(_list[_failureRow]);
		}
		return selectInfo;
	}

	private void LoadTalkAndListenAnger(int _stage, List<Row> _list, ref int _row)
	{
		foreach (KeyValuePair<int, List<BasicInfo>> item in GetDictionaryGroup(_stage, Group.Talk))
		{
			item.Value.Add(CreateGeneric(_list, _row));
		}
		foreach (KeyValuePair<int, List<BasicInfo>> item2 in GetDictionaryGroup(_stage, Group.Listen))
		{
			item2.Value.Add(CreateGeneric(_list, _row));
		}
		_row++;
	}

	private void LoadTouch(int _stage, Group _group, int _num, List<Row> _list, ref int _row)
	{
		int row = _row + N("native.layout.3") * _num;
		for (int i = N("native.layout.0"); i < _num; i++)
		{
			List<BasicInfo> listCommand = GetListCommand(_stage, _group, i);
			for (int j = N("native.layout.0"); j < N("native.layout.3"); j++)
			{
				listCommand.Add(CreateGeneric(_list, _row++));
			}
			listCommand.Add(CreateGeneric(_list, row));
		}
		_row++;
	}

	private void LoadEvent(int _stage, int _command, int _b, int _s, List<Row> _list, ref int _row)
	{
		List<BasicInfo> listCommand = GetListCommand(_stage, Group.Event, _command);
		for (int i = N("native.layout.0"); i < _b; i++)
		{
			listCommand.Add(CreateBranch(_list, ref _row, N("native.layout.1"), _row + N("native.layout.2")));
			_row++;
		}
		for (int j = N("native.layout.0"); j < _s; j++)
		{
			listCommand.Add(CreateSelect(_list, ref _row, N("native.layout.2")));
		}
	}

	private void ParseNpc(List<Row> _rows)
	{
		dicTalkInfo = new Dictionary<int, Dictionary<Group, Dictionary<int, List<BasicInfo>>>>();
		int row = N("native.layout.0");
		List<Row> lst = _rows;
		LoadGeneric(N("native.layout.0"), Group.Introduction, N("native.layout.3"));
		LoadGenericLen(N("native.layout.0"), Group.Listen, N("native.layout.3"), N("native.layout.2"));
		LoadGeneric(N("native.layout.0"), Group.EndConversation, N("native.layout.1"));
		LoadGeneric(N("native.layout.1"), Group.Introduction, N("native.layout.6"));
		LoadGenericLen(N("native.layout.1"), Group.Listen, N("native.layout.3"), N("native.layout.2"));
		LoadGeneric(N("native.layout.1"), Group.EndConversation, N("native.layout.1"));
		LoadGeneric(N("native.layout.2"), Group.Introduction, N("native.layout.6"));
		LoadGenericLen(N("native.layout.2"), Group.Listen, N("native.layout.4"), N("native.layout.2"));
		LoadGeneric(N("native.layout.2"), Group.EndConversation, N("native.layout.1"));
		LoadGeneric(N("native.layout.3"), Group.Introduction, N("native.layout.6"));
		LoadGenericLen(N("native.layout.3"), Group.Listen, N("native.layout.4"), N("native.layout.2"));
		LoadGeneric(N("native.layout.3"), Group.EndConversation, N("native.layout.1"));
		LoadGenericADV(N("native.layout.0"), Group.RankUp, N("native.layout.0"), N("native.layout.11"));
		LoadGenericADV(N("native.layout.1"), Group.RankUp, N("native.layout.0"), N("native.layout.10"));
		LoadGenericADV(N("native.layout.2"), Group.RankUp, N("native.layout.0"), N("native.layout.15"));
		LoadGenericLen(N("native.layout.0"), Group.Talk, N("native.layout.7"), N("native.layout.2"));
		LoadGenericLen(N("native.layout.1"), Group.Talk, N("native.layout.8"), N("native.layout.2"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.3"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.5"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.3"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.0"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.3"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.5"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.3"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.1"), Group.Tips, N("native.layout.0"), N("native.layout.4"));
		LoadGenericADV(N("native.layout.3"), Group.Event, N("native.layout.0"), N("native.layout.8"));
		LoadGenericADV(N("native.layout.3"), Group.Event, N("native.layout.1"), N("native.layout.2"));
		LoadGenericADV(N("native.layout.3"), Group.Event, N("native.layout.8"), N("native.layout.2"));
		LoadGenericADV(N("native.layout.0"), Group.See, N("native.layout.0"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.0"), Group.See, N("native.layout.1"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.0"), Group.Touch, N("native.layout.0"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.0"), Group.Touch, N("native.layout.1"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.0"), Group.Touch, N("native.layout.2"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.0"), Group.Touch, N("native.layout.3"), N("native.layout.1"));
		LoadGeneric(N("native.layout.0"), Group.LeaveAlone, N("native.layout.1"));
		LoadGenericADV(N("native.layout.1"), Group.See, N("native.layout.0"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.1"), Group.See, N("native.layout.1"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.1"), Group.Touch, N("native.layout.0"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.1"), Group.Touch, N("native.layout.1"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.1"), Group.Touch, N("native.layout.2"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.1"), Group.Touch, N("native.layout.3"), N("native.layout.1"));
		LoadGeneric(N("native.layout.1"), Group.LeaveAlone, N("native.layout.1"));
		LoadGenericADV(N("native.layout.3"), Group.See, N("native.layout.0"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.3"), Group.See, N("native.layout.1"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.3"), Group.Touch, N("native.layout.0"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.3"), Group.Touch, N("native.layout.1"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.3"), Group.Touch, N("native.layout.2"), N("native.layout.1"));
		LoadGenericADV(N("native.layout.3"), Group.Touch, N("native.layout.3"), N("native.layout.1"));
		LoadGeneric(N("native.layout.3"), Group.LeaveAlone, N("native.layout.1"));

		void LoadGeneric(int _stage, Group _group, int _num)
		{
			List<BasicInfo> listCommand3 = GetListCommand(_stage, _group, N("native.layout.0"));
			for (int l = N("native.layout.0"); l < _num; l++)
			{
				Row param3 = lst[row];
				listCommand3.Add(new GenericInfo(row, param3)
				{
					priority = param3.priority,
					conditions = param3.conditions
				});
				row++;
			}
		}
		void LoadGenericADV(int _stage, Group _group, int _command, int _len)
		{
			List<BasicInfo> listCommand = GetListCommand(_stage, _group, _command);
			GenericInfo genericInfo = new GenericInfo(row, _len);
			for (int i = N("native.layout.0"); i < _len; i++)
			{
				Row param = lst[row++];
				if (i == N("native.layout.0"))
				{
					genericInfo.priority = param.priority;
					genericInfo.conditions = param.conditions;
				}
				genericInfo.talk[i] = param;
			}
			listCommand.Add(genericInfo);
		}
		void LoadGenericLen(int _stage, Group _group, int _num, int _len)
		{
			List<BasicInfo> listCommand2 = GetListCommand(_stage, _group, N("native.layout.0"));
			for (int j = N("native.layout.0"); j < _num; j++)
			{
				GenericInfo genericInfo2 = new GenericInfo(row, _len);
				for (int k = N("native.layout.0"); k < _len; k++)
				{
					Row param2 = lst[row++];
					if (k == N("native.layout.0"))
					{
						genericInfo2.priority = param2.priority;
						genericInfo2.conditions = param2.conditions;
					}
					genericInfo2.talk[k] = param2;
				}
				listCommand2.Add(genericInfo2);
			}
		}
		}
}
}
