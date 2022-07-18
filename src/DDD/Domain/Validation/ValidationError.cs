﻿namespace DDD.Domain.Validation
{
	public class ValidationError
	{
		public string Key { get; set; }
		public string Details { get; set; }

		public override string ToString()
			=> $"{Key}: {Details}";
	}
}
