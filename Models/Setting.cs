using System;
using System.Text;

namespace Donuts.Models;

public class Setting<T>
{
	public event Action<object> OnSettingChanged;

	//private bool hasLoggedError = false;
	private string _toolTipText;
	private T _settingValue;

	public string Name { get; set; }

	public string ToolTipText
	{
		get => _toolTipText;
		set => _toolTipText = InsertCarriageReturns(value, 50);
	}

	public T Value
	{
		get => _settingValue;
		set
		{
			_settingValue = value;
			OnSettingChanged?.Invoke(this);
		}
	}

	public T DefaultValue { get; set; }
	public T MinValue { get; set; }
	public T MaxValue { get; set; }
	public T[] Options { get; set; }

	// Constructor to handle regular settings
	public Setting(
		string name,
		string tooltipText,
		T value,
		T defaultValue,
		T minValue = default,
		T maxValue = default,
		T[] options = null)
	{
		Name = name;
		ToolTipText = tooltipText;
		Value = value;
		DefaultValue = defaultValue;
		MinValue = minValue;
		MaxValue = maxValue;
		Options = options ?? [];
	}

	public bool OptionsInvalid()
	{
		if (Options != null && Options.Length > 0)
		{
			return false;
		}

		DonutsPlugin.Logger.LogError($"Dropdown setting '{Name}' has an uninitialized or empty options list.");
		//hasLoggedError = true;
		return true;
	}

	private static string InsertCarriageReturns(string text, int maxLength)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
		{
			return text;
		}

		StringBuilder formattedText = new();
		var start = 0;

		while (start < text.Length)
		{
			int end = Math.Min(start + maxLength, text.Length);
			if (end < text.Length && text[end] != ' ')
			{
				int lastSpace = text.LastIndexOf(' ', end, end - start);
				if (lastSpace > start)
				{
					end = lastSpace;
				}
			}

			formattedText.AppendLine(text.Substring(start, end - start).Trim());
			start = end + 1;
		}

		return formattedText.ToString().Trim();
	}
}