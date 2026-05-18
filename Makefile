.PHONY: up down redo clean

up:
	docker compose up -d

down:
	docker compose down

redo: down up

clean:
	docker compose down --volumes --remove-orphans
	docker volume prune -f
	docker image prune -f
