using System;

namespace MonoGame.Tools.Pipeline
{
	public interface IOutput
	{
		void Append (string text);

		void Clear();

	}
}

