using System;

namespace reachstacker {
	public enum State {
		Start,
		InFrontOfContainer,
		PickedUpContainerFromLkw,
		PickedUpContainerFromStorage,
		InFrontOfLkw,
		DepositedContainerOnLkw,
		DepositedContainerOnStorage
	}
}

